using System.IO.Compression;
using System.Text.Json;
using System.ServiceProcess;
using System.Diagnostics;
using System.ComponentModel;
using Teacher.Common.Contracts;

var options = ParseArgs(args);
var logPath = Path.Combine(options.BackupDirectory, $"updater-{DateTime.UtcNow:yyyyMMdd-HHmmss}.log");
Directory.CreateDirectory(options.BackupDirectory);
WriteStatus(options, AgentUpdateStateKind.Installing, "Updater started.", rollbackPerformed: false);

try
{
    Log(logPath, $"Preparing update to {options.TargetVersion}.");
    StopService(options.ServiceName, logPath);
    StopUiHostProcesses(options.InstallDirectory, logPath);

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
    try
    {
        CopyDirectory(
            stagingDirectory,
            options.InstallDirectory,
            skipPredicate: static path =>
            {
                var fileName = Path.GetFileName(path);
                return fileName.Equals("appsettings.json", StringComparison.OrdinalIgnoreCase)
                    || fileName.StartsWith("StudentAgent.Updater", StringComparison.OrdinalIgnoreCase);
            });

        EnsureFirewallRules(options.InstallDirectory, logPath);
        StartService(options.ServiceName, logPath);
        Directory.Delete(stagingDirectory, recursive: true);
        Log(logPath, $"Update to {options.TargetVersion} completed successfully.");
        WriteStatus(options, AgentUpdateStateKind.Succeeded, $"Updated to {options.TargetVersion}.", rollbackPerformed: false);
        return 0;
    }
    catch (Exception installEx)
    {
        Log(logPath, $"Update install failed. Starting rollback: {installEx}");
        WriteStatus(options, AgentUpdateStateKind.Failed, $"Install failed: {installEx.Message}", rollbackPerformed: false);
        RestoreBackup(backupDirectory, options.InstallDirectory, logPath);
        EnsureFirewallRules(options.InstallDirectory, logPath);
        StartService(options.ServiceName, logPath);
        WriteStatus(options, AgentUpdateStateKind.RolledBack, $"Rolled back after failed update to {options.TargetVersion}.", rollbackPerformed: true);
        return 1;
    }
}
catch (Exception ex)
{
    Log(logPath, $"Update failed: {ex}");
    WriteStatus(options, AgentUpdateStateKind.Failed, ex.Message, rollbackPerformed: false);
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
        CopyFileWithRetries(file, destinationPath);
    }
}

static void CopyFileWithRetries(string sourcePath, string destinationPath)
{
    const int maxAttempts = 8;

    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            if (File.Exists(destinationPath))
            {
                File.SetAttributes(destinationPath, FileAttributes.Normal);
            }

            File.Copy(sourcePath, destinationPath, overwrite: true);
            return;
        }
        catch (IOException) when (attempt < maxAttempts)
        {
            Thread.Sleep(750);
        }
        catch (UnauthorizedAccessException) when (attempt < maxAttempts)
        {
            Thread.Sleep(750);
        }
    }

    if (File.Exists(destinationPath))
    {
        File.SetAttributes(destinationPath, FileAttributes.Normal);
    }

    File.Copy(sourcePath, destinationPath, overwrite: true);
}

static void RestoreBackup(string backupDirectory, string installDirectory, string logPath)
{
    if (!Directory.Exists(backupDirectory))
    {
        throw new InvalidOperationException($"Rollback backup was not found at '{backupDirectory}'.");
    }

    Log(logPath, $"Restoring backup from '{backupDirectory}'.");
    StopUiHostProcesses(installDirectory, logPath);
    CopyDirectory(
        backupDirectory,
        installDirectory,
        skipPredicate: static path => Path.GetFileName(path).StartsWith("StudentAgent.Updater", StringComparison.OrdinalIgnoreCase));
}

static void EnsureFirewallRules(string installDirectory, string logPath)
{
    EnsureFirewallRule(
        "ClassCommander StudentAgent Service",
        Path.Combine(installDirectory, "StudentAgent.Service.exe"),
        "Allows the StudentAgent service to accept classroom control connections.",
        logPath);

    EnsureFirewallRule(
        "ClassCommander StudentAgent VNC Host",
        Path.Combine(installDirectory, "StudentAgent.VncHost.exe"),
        "Allows the student VNC host to accept remote management connections.",
        logPath);
}

static void EnsureFirewallRule(string ruleName, string programPath, string description, string logPath)
{
    if (!File.Exists(programPath))
    {
        Log(logPath, $"Skipping firewall rule '{ruleName}' because '{programPath}' was not found.");
        return;
    }

    RunNetsh($"advfirewall firewall delete rule name=\"{ruleName}\" program=\"{programPath}\"", logPath, ignoreFailure: true);
    RunNetsh(
        $"advfirewall firewall add rule name=\"{ruleName}\" dir=in action=allow profile=any program=\"{programPath}\" enable=yes description=\"{description}\"",
        logPath);
}

static void RunNetsh(string arguments, string logPath, bool ignoreFailure = false)
{
    using var process = Process.Start(new ProcessStartInfo
    {
        FileName = "netsh",
        Arguments = arguments,
        CreateNoWindow = true,
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true
    });

    if (process is null)
    {
        throw new InvalidOperationException("Failed to start netsh.");
    }

    process.WaitForExit();
    var standardOutput = process.StandardOutput.ReadToEnd();
    var standardError = process.StandardError.ReadToEnd();

    if (!string.IsNullOrWhiteSpace(standardOutput))
    {
        Log(logPath, $"netsh stdout: {standardOutput.Trim()}");
    }

    if (!string.IsNullOrWhiteSpace(standardError))
    {
        Log(logPath, $"netsh stderr: {standardError.Trim()}");
    }

    if (process.ExitCode != 0 && !ignoreFailure)
    {
        throw new Win32Exception(process.ExitCode, $"netsh exited with code {process.ExitCode} while running '{arguments}'.");
    }
}

static void StopUiHostProcesses(string installDirectory, string logPath)
{
    var normalizedInstallDirectory = Path.GetFullPath(installDirectory)
        .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    foreach (var process in Process.GetProcessesByName("StudentAgent.UIHost"))
    {
        using (process)
        {
            try
            {
                if (process.HasExited)
                {
                    continue;
                }

                string? processPath;
                try
                {
                    processPath = process.MainModule?.FileName;
                }
                catch
                {
                    processPath = null;
                }

                if (!string.IsNullOrWhiteSpace(processPath))
                {
                    var normalizedProcessPath = Path.GetFullPath(processPath);
                    if (!normalizedProcessPath.StartsWith(normalizedInstallDirectory, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }

                Log(logPath, $"Stopping StudentAgent.UIHost process {process.Id}.");
                process.Kill(entireProcessTree: true);
                process.WaitForExit((int)TimeSpan.FromSeconds(30).TotalMilliseconds);
            }
            catch (Exception ex)
            {
                Log(logPath, $"Failed to stop StudentAgent.UIHost process {process.Id}: {ex}");
            }
        }
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

static void WriteStatus(UpdaterOptions options, AgentUpdateStateKind state, string message, bool rollbackPerformed)
{
    Directory.CreateDirectory(Path.GetDirectoryName(options.StatusPath)!);
    var payload = new UpdaterStatusFile(
        state,
        options.TargetVersion,
        message,
        rollbackPerformed,
        DateTime.UtcNow);
    File.WriteAllText(options.StatusPath, JsonSerializer.Serialize(payload, new JsonSerializerOptions
    {
        WriteIndented = true
    }));
}

internal sealed record UpdaterOptions(
    string ServiceName,
    string ZipPath,
    string InstallDirectory,
    string BackupDirectory,
    string TargetVersion)
{
    public string StatusPath => Path.Combine(Path.GetDirectoryName(BackupDirectory)!, "update-status.json");
}

internal sealed record UpdaterStatusFile(
    AgentUpdateStateKind State,
    string TargetVersion,
    string Message,
    bool RollbackPerformed,
    DateTime UpdatedAtUtc);
