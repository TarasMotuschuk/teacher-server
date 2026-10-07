using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using StudentAgent.Services;
using Teacher.Common.Contracts;

namespace StudentAgent.Service.Services;

public sealed class RemoteCommandService
{
    private readonly AgentLogService _logService;
    private readonly string _scriptsDirectory;

    public RemoteCommandService(AgentLogService logService)
    {
        _logService = logService;

        _scriptsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "TeacherServer.RemoteCommands");
        var directory = Directory.CreateDirectory(_scriptsDirectory);
        if (OperatingSystem.IsWindows())
        {
            if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("The remote-command directory cannot be a reparse point.");
            // A command executed as SYSTEM must never inherit student write access from the runtime root.
            var security = new DirectorySecurity();
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            security.SetOwner(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null));
            foreach (var sid in new[] { WellKnownSidType.LocalSystemSid, WellKnownSidType.BuiltinAdministratorsSid })
                security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(sid, null), FileSystemRights.FullControl,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null), FileSystemRights.ReadAndExecute,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
            directory.SetAccessControl(security);
        }
    }

    public string ExecuteScript(string script, RemoteCommandRunAs runAs)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Remote command execution is only supported on Windows student agents.");
        }

        if (!Enum.IsDefined(runAs))
            throw new ArgumentOutOfRangeException(nameof(runAs));
        var normalizedScript = NormalizeScript(script);
        if (string.IsNullOrWhiteSpace(normalizedScript))
        {
            throw new ArgumentException("Command script is required.", nameof(script));
        }

        var scriptPath = WriteScriptFile(normalizedScript);
        try
        {
            if (runAs == RemoteCommandRunAs.CurrentUser)
            {
                var sessionId = SessionProcessLauncher.GetActiveSessionId();
                if (sessionId < 0)
                {
                    throw new InvalidOperationException("No active interactive user session was found.");
                }

                SessionProcessLauncher.StartCmdScriptInSession(scriptPath, sessionId);
                _logService.LogInfo($"Executed remote command script as current user in session {sessionId}: {scriptPath}");
                return $"current-user:{sessionId}";
            }

            SessionProcessLauncher.StartCmdScriptAsAdministrator(scriptPath);
            _logService.LogInfo($"Executed remote command script as administrator: {scriptPath}");
            return "administrator";
        }
        catch
        {
            File.Delete(scriptPath);
            throw;
        }
    }

    private static string NormalizeScript(string script)
    {
        var lines = script
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(line => line.TrimEnd())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        return string.Join(Environment.NewLine, lines);
    }

    private string WriteScriptFile(string script)
    {
        var filePath = Path.Combine(_scriptsDirectory, $"remote-command-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.cmd");
        var content = new StringBuilder()
            .AppendLine("@echo off")
            .AppendLine("chcp 65001 >nul")
            .AppendLine("setlocal")
            .AppendLine(script)
            .AppendLine("del \"%~f0\" >nul 2>nul")
            .ToString();

        using (var stream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        using (var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
        {
            writer.Write(content);
        }
        if (OperatingSystem.IsWindows())
        {
            var file = new FileInfo(filePath);
            var security = file.GetAccessControl();
            security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
                FileSystemRights.ReadAndExecute | FileSystemRights.Delete, AccessControlType.Allow));
            file.SetAccessControl(security);
        }
        return filePath;
    }
}
