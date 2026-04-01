using System.IO.Compression;
using System.ServiceProcess;

var options = ParseArgs(args);
var logPath = Path.Combine(options.BackupDirectory, $"updater-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log");
Directory.CreateDirectory(options.BackupDirectory);

try
{
    Log(logPath, $"Preparing update to {options.TargetVersion}.");
    StopService(options.ServiceName, logPath);

    var stagingDirectory = Path.Combine(Path.GetTempPath(), $"StudentAgentUpdate-{Guid.NewGuid():N}");
    if (Directory.Exists(stagingDirectory))
    {
        Directory.Delete(stagingDirectory, recursive: true);
    }

    Directory.CreateDirectory(stagingDirectory);
    ZipFile.ExtractToDirectory(options.ZipPath, stagingDirectory, overwriteFiles: true);

    var backupDirectory = Path.Combine(options.BackupDirectory, options.TargetVersion);
    if (Directory.Exists(backupDirectory))
    {
        Directory.Delete(backupDirectory, recursive: true);
    }

    CopyDirectory(options.InstallDirectory, backupDirectory, skipPredicate: static path => Path.GetFileName(path).Equals("appsettings.json", StringComparison.OrdinalIgnoreCase));
    CopyDirectory(
        stagingDirectory,
        options.InstallDirectory,
        skipPredicate: static path =>
        {
            var fileName = Path.GetFileName(path);
            return fileName.Equals("appsettings.json", StringComparison.OrdinalIgnoreCase)
                || fileName.StartsWith("StudentAgent.Updater", StringComparison.OrdinalIgnoreCase);
        });

    StartService(options.ServiceName, logPath);
    Directory.Delete(stagingDirectory, recursive: true);
    Log(logPath, $"Update to {options.TargetVersion} completed successfully.");
    return 0;
}
catch (Exception ex)
{
    Log(logPath, $"Update failed: {ex}");
    try
    {
        StartService(options.ServiceName, logPath);
    }
    catch (Exception restartEx)
    {
        Log(logPath, $"Failed to restart service after update error: {restartEx}");
    }

    return 1;
}

static void StopService(string serviceName, string logPath)
{
    using var controller = new ServiceController(serviceName);
    controller.Refresh();
    if (controller.Status == ServiceControllerStatus.Stopped)
    {
        return;
    }

    Log(logPath, $"Stopping service {serviceName}.");
    controller.Stop();
    controller.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromMinutes(2));
}

static void StartService(string serviceName, string logPath)
{
    using var controller = new ServiceController(serviceName);
    controller.Refresh();
    if (controller.Status == ServiceControllerStatus.Running)
    {
        return;
    }

    Log(logPath, $"Starting service {serviceName}.");
    controller.Start();
    controller.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromMinutes(2));
}

static void CopyDirectory(string sourceDirectory, string destinationDirectory, Func<string, bool> skipPredicate)
{
    Directory.CreateDirectory(destinationDirectory);

    foreach (var directory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
    {
        var relativePath = Path.GetRelativePath(sourceDirectory, directory);
        Directory.CreateDirectory(Path.Combine(destinationDirectory, relativePath));
    }

    foreach (var file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
    {
        if (skipPredicate(file))
        {
            continue;
        }

        var relativePath = Path.GetRelativePath(sourceDirectory, file);
        var destinationPath = Path.Combine(destinationDirectory, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        File.Copy(file, destinationPath, overwrite: true);
    }
}

static void Log(string logPath, string message)
{
    File.AppendAllText(logPath, $"[{DateTime.UtcNow:O}] {message}{Environment.NewLine}");
}

static UpdaterOptions ParseArgs(string[] args)
{
    var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var i = 0; i < args.Length - 1; i += 2)
    {
        values[args[i]] = args[i + 1];
    }

    return new UpdaterOptions(
        GetRequired(values, "--service-name"),
        GetRequired(values, "--zip"),
        GetRequired(values, "--install-dir"),
        GetRequired(values, "--backup-dir"),
        GetRequired(values, "--target-version"));
}

static string GetRequired(IReadOnlyDictionary<string, string> values, string key)
    => values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
        ? value
        : throw new InvalidOperationException($"Missing required argument {key}.");

internal sealed record UpdaterOptions(
    string ServiceName,
    string ZipPath,
    string InstallDirectory,
    string BackupDirectory,
    string TargetVersion);
