namespace ClassCommander.TestPlatform.Storage;

internal sealed class TestPlatformPaths
{
    public TestPlatformPaths(string rootDirectory)
    {
        RootDirectory = Path.GetFullPath(rootDirectory);
        DatabasePath = Path.Combine(RootDirectory, "testplatform.sqlite");
        TestsDirectory = Path.Combine(RootDirectory, "tests");
    }

    public string RootDirectory { get; }

    public string DatabasePath { get; }

    public string TestsDirectory { get; }

    public string GetTestDirectory(string testPublicId) =>
        Path.Combine(TestsDirectory, SanitizeSegment(testPublicId));

    public string GetAssetsDirectory(string testPublicId) =>
        Path.Combine(GetTestDirectory(testPublicId), "assets");

    public string GetImportsDirectory(string testPublicId) =>
        Path.Combine(GetTestDirectory(testPublicId), "imports");

    public void EnsureCreated()
    {
        Directory.CreateDirectory(RootDirectory);
        Directory.CreateDirectory(TestsDirectory);
    }

    private static string SanitizeSegment(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        var sanitized = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "test" : sanitized;
    }
}
