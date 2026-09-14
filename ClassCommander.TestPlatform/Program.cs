using Teacher.Common.Contracts.Testing;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    Product = "ClassCommander.TestPlatform",
    Purpose = "Test server and results management shell.",
    Version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0",
}));

app.MapGet("/health", () => Results.Ok(new
{
    Status = "ok",
    Service = "ClassCommander.TestPlatform",
}));

var tests = app.MapGroup("/api/tests/v1");

tests.MapGet("/definitions", () =>
{
    var response = new PagedResponseDto<TestDefinitionListItemDto>(
        [],
        0);
    return Results.Ok(response);
});

tests.MapGet("/capabilities", () => Results.Ok(new
{
    HandlesDefinitions = true,
    HandlesAssignments = false,
    HandlesAttempts = false,
    HandlesResults = false,
}));

app.Run();
