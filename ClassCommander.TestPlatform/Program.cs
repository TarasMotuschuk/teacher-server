using ClassCommander.Testing.Core.Import;
using ClassCommander.Testing.Core.Packaging;
using ClassCommander.Testing.Core.Serialization;
using ClassCommander.TestPlatform.Scoring;
using ClassCommander.TestPlatform.Storage;
using Teacher.Common.Contracts.Testing;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = TestPlatformJson.Options.PropertyNamingPolicy;
    options.SerializerOptions.DictionaryKeyPolicy = TestPlatformJson.Options.DictionaryKeyPolicy;
    options.SerializerOptions.DefaultIgnoreCondition = TestPlatformJson.Options.DefaultIgnoreCondition;
    foreach (var converter in TestPlatformJson.Options.Converters)
    {
        options.SerializerOptions.Converters.Add(converter);
    }
});

var configuredRoot = builder.Configuration["TestPlatform:DataRoot"];
var dataRoot = string.IsNullOrWhiteSpace(configuredRoot)
    ? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ClassCommander",
        "TestPlatform")
    : configuredRoot;

var paths = new TestPlatformPaths(dataRoot);
paths.EnsureCreated();
var db = new TestPlatformDb(paths);
var repository = new TestPlatformRepository(db);
var importer = new MyTestXmlImporter();
var packageService = new CctestPackageService();

builder.WebHost.UseUrls(
    Environment.GetEnvironmentVariable("ASPNETCORE_URLS")
    ?? "http://0.0.0.0:5050");

builder.Services.AddSingleton(paths);
builder.Services.AddSingleton(db);
builder.Services.AddSingleton(repository);
builder.Services.AddSingleton(importer);
builder.Services.AddSingleton(packageService);

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    Product = "ClassCommander.TestPlatform",
    Purpose = "Local classroom test server.",
    Version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0",
    DataRoot = paths.RootDirectory,
}));

app.MapGet("/health", () => Results.Ok(new
{
    Status = "ok",
    Service = "ClassCommander.TestPlatform",
}));

var tests = app.MapGroup("/api/tests/v1");

tests.MapGet("/capabilities", () => Results.Ok(new
{
    HandlesDefinitions = true,
    HandlesAssignments = true,
    HandlesAttempts = true,
    HandlesResults = true,
    HandlesMyTestImport = true,
    HandlesCctestImport = true,
}));

tests.MapGet("/test-definitions", (string? search, string? grade, string? subject, string? status) =>
{
    TestStatus? parsedStatus = null;
    if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<TestStatus>(status, ignoreCase: true, out var value))
    {
        parsedStatus = value;
    }

    return Results.Ok(repository.ListDefinitions(search, grade, subject, parsedStatus));
});

tests.MapGet("/test-definitions/{testId}", (string testId, int? version) =>
{
    var definition = repository.GetDefinition(testId, version);
    return definition is null ? Results.NotFound() : Results.Ok(definition);
});

tests.MapPost("/test-definitions", (TestDefinitionDto definition) =>
{
    if (string.IsNullOrWhiteSpace(definition.PublicId))
    {
        return Results.BadRequest(new { error = "publicId is required." });
    }

    var saved = repository.UpsertDefinition(definition, TestStatus.Draft);
    return Results.Created($"/api/tests/v1/test-definitions/{saved.PublicId}?version={saved.Version}", saved);
});

tests.MapPut("/test-definitions/{testId}", (string testId, TestDefinitionDto definition) =>
{
    if (!string.Equals(testId, definition.PublicId, StringComparison.OrdinalIgnoreCase))
    {
        return Results.BadRequest(new { error = "Route testId must match body publicId." });
    }

    var saved = repository.UpsertDefinition(definition, TestStatus.Draft);
    return Results.Ok(saved);
});

tests.MapPost("/imports/mytest-xml", async (HttpRequest request) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { error = "Expected multipart/form-data with a file field named 'file'." });
    }

    var form = await request.ReadFormAsync();
    var file = form.Files.GetFile("file") ?? (form.Files.Count > 0 ? form.Files[0] : null);
    if (file is null || file.Length == 0)
    {
        return Results.BadRequest(new { error = "MyTest XML file is required." });
    }

    var workDir = Path.Combine(Path.GetTempPath(), "ClassCommander", "imports", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(workDir);
    await using var stream = file.OpenReadStream();
    var (definition, warnings) = importer.Import(stream, file.FileName, workDir);

    var testDir = paths.GetTestDirectory(definition.PublicId);
    Directory.CreateDirectory(testDir);
    var assetsTarget = paths.GetAssetsDirectory(definition.PublicId);
    Directory.CreateDirectory(assetsTarget);
    var workAssets = Path.Combine(workDir, "assets");
    if (Directory.Exists(workAssets))
    {
        foreach (var assetFile in Directory.EnumerateFiles(workAssets))
        {
            File.Copy(assetFile, Path.Combine(assetsTarget, Path.GetFileName(assetFile)), overwrite: true);
        }
    }

    // Persist package-relative asset paths as server-relative under tests/{id}/...
    definition = definition with
    {
        Assets = definition.Assets
            .Select(asset => asset with
            {
                Path = Path.Combine("tests", definition.PublicId, "assets", Path.GetFileName(asset.Path)).Replace('\\', '/'),
            })
            .ToList(),
    };

    var importsDirectory = paths.GetImportsDirectory(definition.PublicId);
    Directory.CreateDirectory(importsDirectory);
    var originalPath = Path.Combine(importsDirectory, Path.GetFileName(file.FileName));
    await using (var persist = File.Create(originalPath))
    {
        await using var copy = file.OpenReadStream();
        await copy.CopyToAsync(persist);
    }

    try
    {
        Directory.Delete(workDir, recursive: true);
    }
    catch
    {
        // Best-effort cleanup.
    }

    var saved = repository.UpsertDefinition(definition, TestStatus.Draft);
    var response = new MyTestImportResponseDto(
        $"import_{Guid.NewGuid():N}",
        "completed",
        saved,
        warnings);
    return Results.Ok(response);
});

tests.MapPost("/imports/cctest", async (HttpRequest request) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { error = "Expected multipart/form-data with a file field named 'file'." });
    }

    var form = await request.ReadFormAsync();
    var file = form.Files.GetFile("file") ?? (form.Files.Count > 0 ? form.Files[0] : null);
    if (file is null || file.Length == 0)
    {
        return Results.BadRequest(new { error = "cctest package file is required." });
    }

    var workDir = Path.Combine(Path.GetTempPath(), "ClassCommander", "cctest-imports", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(workDir);
    var packagePath = Path.Combine(workDir, Path.GetFileName(file.FileName));
    if (string.IsNullOrWhiteSpace(Path.GetExtension(packagePath)))
    {
        packagePath += ".cctest";
    }

    await using (var persistPackage = File.Create(packagePath))
    {
        await using var upload = file.OpenReadStream();
        await upload.CopyToAsync(persistPackage);
    }

    string extractedDirectory;
    TestDefinitionDto definition;
    try
    {
        (_, definition, extractedDirectory) = await packageService.OpenAsync(packagePath);
    }
    catch (Exception ex)
    {
        try
        {
            Directory.Delete(workDir, recursive: true);
        }
        catch
        {
            // Best-effort cleanup.
        }

        return Results.BadRequest(new { error = ex.Message });
    }

    var testDir = paths.GetTestDirectory(definition.PublicId);
    Directory.CreateDirectory(testDir);
    var assetsTarget = paths.GetAssetsDirectory(definition.PublicId);
    Directory.CreateDirectory(assetsTarget);
    var packageAssets = Path.Combine(extractedDirectory, "assets");
    if (Directory.Exists(packageAssets))
    {
        foreach (var assetFile in Directory.EnumerateFiles(packageAssets))
        {
            File.Copy(assetFile, Path.Combine(assetsTarget, Path.GetFileName(assetFile)), overwrite: true);
        }
    }

    definition = definition with
    {
        Assets = definition.Assets
            .Select(asset => asset with
            {
                Path = Path.Combine("tests", definition.PublicId, "assets", Path.GetFileName(asset.Path)).Replace('\\', '/'),
            })
            .ToList(),
    };

    var importsDirectory = paths.GetImportsDirectory(definition.PublicId);
    Directory.CreateDirectory(importsDirectory);
    var storedPackagePath = Path.Combine(importsDirectory, Path.GetFileName(packagePath));
    File.Copy(packagePath, storedPackagePath, overwrite: true);

    try
    {
        Directory.Delete(workDir, recursive: true);
        Directory.Delete(extractedDirectory, recursive: true);
    }
    catch
    {
        // Best-effort cleanup.
    }

    var saved = repository.UpsertDefinition(definition, TestStatus.Draft);
    var response = new MyTestImportResponseDto(
        $"import_{Guid.NewGuid():N}",
        "completed",
        saved,
        []);
    return Results.Ok(response);
});

tests.MapGet("/assignments", (string? status, string? testId) =>
{
    AssignmentStatus? parsedStatus = null;
    if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<AssignmentStatus>(status, ignoreCase: true, out var value))
    {
        parsedStatus = value;
    }

    return Results.Ok(repository.ListAssignments(parsedStatus, testId));
});

tests.MapPost("/assignments", (CreateAssignmentRequest request) =>
{
    try
    {
        var assignment = repository.CreateAssignment(request);
        return Results.Created($"/api/tests/v1/assignments/{assignment.PublicId}", assignment);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

tests.MapGet("/assignments/{assignmentId}", (string assignmentId) =>
{
    var assignment = repository.GetAssignment(assignmentId);
    return assignment is null ? Results.NotFound() : Results.Ok(assignment);
});

tests.MapPost("/assignments/{assignmentId}/publish", (string assignmentId) =>
{
    var assignment = repository.SetAssignmentStatus(assignmentId, AssignmentStatus.Published);
    return assignment is null ? Results.NotFound() : Results.Ok(assignment);
});

tests.MapPost("/assignments/{assignmentId}/close", (string assignmentId) =>
{
    var assignment = repository.SetAssignmentStatus(assignmentId, AssignmentStatus.Closed);
    return assignment is null ? Results.NotFound() : Results.Ok(assignment);
});

tests.MapGet("/assignments/{assignmentId}/attempts", (string assignmentId) =>
    Results.Ok(repository.ListAttempts(assignmentId)));

tests.MapGet("/assignments/{assignmentId}/results", (string assignmentId) =>
{
    var attempts = repository.ListAttempts(assignmentId).Items;
    var results = attempts
        .Select(item => repository.GetResult(item.AttemptPublicId))
        .Where(result => result is not null)
        .Cast<ResultDto>()
        .ToList();
    return Results.Ok(new PagedResponseDto<ResultDto>(results, results.Count));
});

tests.MapGet("/attempts/{attemptId}", (string attemptId) =>
{
    var attempt = repository.GetAttempt(attemptId);
    return attempt is null ? Results.NotFound() : Results.Ok(attempt);
});

tests.MapGet("/attempts/{attemptId}/result", (string attemptId) =>
{
    var result = repository.GetResult(attemptId);
    return result is null ? Results.NotFound() : Results.Ok(result);
});

tests.MapPost("/student/resolve", (ResolveStudentRequest request) =>
{
    var student = new AttemptStudentDto(
        StudentPublicId: null,
        request.Surname,
        request.Name,
        request.Middlename,
        request.ClassName,
        request.DeviceId);
    var active = repository.ListActiveAssignmentsForStudent(student);
    return Results.Ok(new ResolveStudentResponse(student, active));
});

tests.MapGet("/student/active-assignment", (string? surname, string? name, string? className, string? deviceId) =>
{
    var student = new AttemptStudentDto(null, surname ?? string.Empty, name ?? string.Empty, null, className, deviceId);
    var active = repository.ListActiveAssignmentsForStudent(student);
    return active.Count == 0 ? Results.NotFound() : Results.Ok(active[0]);
});

tests.MapPost("/student/attempts", (StartAttemptRequest request) =>
{
    try
    {
        var (attempt, token) = repository.StartOrResumeAttempt(request);
        var assignment = repository.GetAssignment(attempt.AssignmentPublicId)
            ?? throw new InvalidOperationException("Assignment disappeared after attempt start.");
        var definition = repository.GetDefinition(attempt.TestPublicId, attempt.TestVersion)
            ?? throw new InvalidOperationException("Test definition was not found for attempt.");

        var response = new StartAttemptResponse(
            attempt.PublicId,
            attempt.Status,
            attempt.StartedAtUtc,
            attempt.LastSavedAtUtc,
            token,
            assignment,
            TestPlatformJson.StripAnswerKeys(definition),
            attempt.Answers);
        return Results.Ok(response);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

tests.MapPut("/student/attempts/{attemptId}/progress", (string attemptId, HttpRequest httpRequest, SaveAttemptProgressRequest body) =>
{
    if (!TryAuthorizeAttempt(httpRequest, attemptId, repository, out var error))
    {
        return error;
    }

    try
    {
        var updated = repository.SaveProgress(attemptId, body.Answers);
        return Results.Ok(new SaveAttemptProgressResponse(updated.PublicId, updated.Status, updated.LastSavedAtUtc ?? DateTime.UtcNow));
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

tests.MapPost("/student/attempts/{attemptId}/submit", (string attemptId, HttpRequest httpRequest, SubmitAttemptRequest body) =>
{
    if (!TryAuthorizeAttempt(httpRequest, attemptId, repository, out var error))
    {
        return error;
    }

    try
    {
        var attempt = repository.GetAttempt(attemptId)
            ?? throw new InvalidOperationException($"Attempt '{attemptId}' was not found.");
        var assignment = repository.GetAssignment(attempt.AssignmentPublicId)
            ?? throw new InvalidOperationException("Assignment was not found.");
        var definition = repository.GetDefinition(attempt.TestPublicId, attempt.TestVersion)
            ?? throw new InvalidOperationException("Test definition was not found.");

        var mergedAnswers = body.Answers.Count == 0 ? attempt.Answers : body.Answers;
        var scored = AttemptScoringService.Score(definition, attemptId, mergedAnswers);
        var (submitted, result) = repository.SubmitAttempt(attemptId, mergedAnswers, scored);

        var viewPolicy = assignment.ResultPolicy;
        var visibleResult = viewPolicy.ShowPerQuestionFeedback
            ? result
            : result with { QuestionResults = viewPolicy.ShowCorrectAnswers ? result.QuestionResults : [] };
        if (!viewPolicy.ShowScore)
        {
            visibleResult = visibleResult with { ScoreEarned = 0, ScoreMax = 0, Percent = 0, Grade = null };
        }

        return Results.Ok(new SubmitAttemptResponse(
            submitted.PublicId,
            submitted.Status,
            submitted.SubmittedAtUtc ?? DateTime.UtcNow,
            visibleResult,
            viewPolicy));
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.Lifetime.ApplicationStopped.Register(() => db.Dispose());
app.Run();

static bool TryAuthorizeAttempt(
    HttpRequest request,
    string attemptId,
    TestPlatformRepository repository,
    out IResult errorResult)
{
    errorResult = Results.Ok();
    if (!request.Headers.TryGetValue("X-Attempt-Token", out var tokenValues)
        || string.IsNullOrWhiteSpace(tokenValues.ToString()))
    {
        errorResult = Results.Unauthorized();
        return false;
    }

    var attempt = repository.GetAttemptByToken(tokenValues.ToString());
    if (attempt is null || !string.Equals(attempt.PublicId, attemptId, StringComparison.OrdinalIgnoreCase))
    {
        errorResult = Results.Unauthorized();
        return false;
    }

    return true;
}
