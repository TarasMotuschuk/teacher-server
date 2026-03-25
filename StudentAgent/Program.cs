using Microsoft.Extensions.Options;
using StudentAgent;
using StudentAgent.Auth;
using StudentAgent.Services;
using Teacher.Common.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AgentOptions>(builder.Configuration.GetSection(AgentOptions.SectionName));
builder.Services.AddSingleton<ProcessService>();
builder.Services.AddSingleton<FileService>();
builder.Services.AddSingleton<ServerInfoService>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();
var agentOptions = app.Services.GetRequiredService<IOptions<AgentOptions>>().Value;

app.Urls.Add($"http://0.0.0.0:{agentOptions.Port}");
app.UseMiddleware<SharedSecretMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok", utc = DateTime.UtcNow }));

app.MapGet("/api/info", (ServerInfoService service) =>
{
    return Results.Ok(service.GetInfo());
});

app.MapGet("/api/processes", (ProcessService service) =>
{
    return Results.Ok(service.GetProcesses());
});

app.MapPost("/api/processes/kill", (KillProcessRequest request, ProcessService service) =>
{
    try
    {
        service.KillProcess(request.ProcessId);
        return Results.NoContent();
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/api/files/roots", (FileService service) =>
{
    return Results.Ok(service.GetRoots());
});

app.MapGet("/api/files/list", (string? path, FileService service) =>
{
    try
    {
        return Results.Ok(service.GetDirectory(path ?? string.Empty));
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapDelete("/api/files", (DeleteEntryRequest request, FileService service) =>
{
    try
    {
        service.DeleteEntry(request.FullPath);
        return Results.NoContent();
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/files/directories", (CreateDirectoryRequest request, FileService service) =>
{
    try
    {
        service.CreateDirectory(request.ParentPath, request.Name);
        return Results.NoContent();
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/api/files/download", (string fullPath, FileService service) =>
{
    try
    {
        var (fileName, stream, contentType) = service.OpenRead(fullPath);
        return Results.File(stream, contentType, fileName);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapPost("/api/files/upload", async (HttpRequest request, FileService service, CancellationToken cancellationToken) =>
{
    if (!request.HasFormContentType)
    {
        return Results.BadRequest(new { error = "multipart/form-data is required." });
    }

    var form = await request.ReadFormAsync(cancellationToken);
    var file = form.Files["file"];
    var destinationDirectory = form["destinationDirectory"].ToString();

    if (file is null)
    {
        return Results.BadRequest(new { error = "File is missing." });
    }

    await using var source = file.OpenReadStream();
    await service.SaveFileAsync(destinationDirectory, file.FileName, source, cancellationToken);
    return Results.NoContent();
});

app.Run();
