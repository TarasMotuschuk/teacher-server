using System.Text.Json;

namespace TeacherClient.CrossPlatform.Services;

internal static class TestExitCodeStore
{
    private static string StoragePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "TeacherServer", "TeacherClient.Avalonia", "test-exit-codes.json");

    public static IReadOnlyList<Entry> Load()
    {
        if (!File.Exists(StoragePath))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<Entry>>(File.ReadAllText(StoragePath)) ?? [];
    }

    public static void Add(string assignment, string code, string machines)
    {
        var entries = Load().ToList();
        entries.Add(new Entry(DateTimeOffset.Now, assignment, code, machines));
        Directory.CreateDirectory(Path.GetDirectoryName(StoragePath)!);
        var temporaryPath = StoragePath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(entries));
        File.Move(temporaryPath, StoragePath, overwrite: true);
    }

    internal sealed record Entry(DateTimeOffset CreatedAt, string Assignment, string Code, string Machines);
}
