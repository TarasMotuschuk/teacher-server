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

        _scriptsDirectory = Path.Combine(StudentAgentPathHelper.GetRootDirectory(), "remote-commands");
        Directory.CreateDirectory(_scriptsDirectory);
    }

    public string ExecuteScript(string script, RemoteCommandRunAs runAs)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Remote command execution is only supported on Windows student agents.");
        }

        var normalizedScript = NormalizeScript(script);
        if (string.IsNullOrWhiteSpace(normalizedScript))
        {
            throw new ArgumentException("Command script is required.", nameof(script));
        }

        var scriptPath = WriteScriptFile(normalizedScript);
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

    private string WriteScriptFile(string script)
    {
        var filePath = Path.Combine(_scriptsDirectory, $"remote-command-{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.cmd");
        var content = new StringBuilder()
            .AppendLine("@echo off")
            .AppendLine("setlocal")
            .AppendLine(script)
            .AppendLine("del \"%~f0\" >nul 2>nul")
            .ToString();

        File.WriteAllText(filePath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return filePath;
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
}
