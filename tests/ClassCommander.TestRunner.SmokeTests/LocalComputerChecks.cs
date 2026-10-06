using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;
using Avalonia.Controls;
using Teacher.Common;
using Teacher.Common.Contracts;
using TeacherClient.CrossPlatform.Services;

namespace ClassCommander.TestRunner.SmokeTests;

internal static class LocalComputerChecks
{
    public static void Run()
    {
        Task.Run(CheckAsync).GetAwaiter().GetResult();
        using var window = new TeacherClient.CrossPlatform.MainWindow();
        var agent = TeacherClient.CrossPlatform.DiscoveredAgentRow.FromKnownEntry(new TeacherClient.CrossPlatform.Models.KnownAgentEntry
        {
            AgentId = "local",
            MachineName = Environment.MachineName,
            RespondingAddress = "127.0.0.1",
            Port = 1,
        });
        foreach (var method in new[] { "ToggleInputLockAsync", "ToggleBrowserLockAsync", "SetInputLockOnAgentsAsync", "SetBrowserLockOnAgentsAsync" })
        {
            var argumentType = method.StartsWith("Toggle", StringComparison.Ordinal)
                ? typeof(TeacherClient.CrossPlatform.DiscoveredAgentRow)
                : typeof(IReadOnlyList<TeacherClient.CrossPlatform.DiscoveredAgentRow>);
            var action = window.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance, [argumentType, typeof(bool)])!;
            object targets = argumentType == typeof(TeacherClient.CrossPlatform.DiscoveredAgentRow) ? agent : new[] { agent };
            var result = (Task)action.Invoke(window, [targets, true])!;
            Require(result.IsCompletedSuccessfully, "Local individual and group locks must stop before confirmation or HTTP.");
            var status = window.FindControl<TextBlock>("StatusTextBlock")!.Text ?? string.Empty;
            Require(status.Contains("захищено", StringComparison.Ordinal) || status.Contains("protected", StringComparison.Ordinal), "Show why the local lock was skipped.");
        }

        window.Close();
        Console.WriteLine("PASS: teacher UI rejects individual and group locking of the local agent.");
    }

    private static async Task CheckAsync()
    {
        foreach (var host in new[] { "127.0.0.1", "127.1.2.3", "::1", "::ffff:127.0.0.1", "localhost", Environment.MachineName })
        {
            Require(await LocalComputerGuard.IsLocalAsync(host), $"Must recognize local endpoint {host}.");
        }

        foreach (var address in NetworkInterface.GetAllNetworkInterfaces().SelectMany(network => network.GetIPProperties().UnicastAddresses))
        {
            Require(await LocalComputerGuard.IsLocalAsync(address.Address.ToString()), "All local interface addresses must be protected.");
        }

        Require(await LocalComputerGuard.IsLocalAsync("192.0.2.1", Environment.MachineName), "A discovered local machine name must be protected.");
        Require(!await LocalComputerGuard.IsLocalAsync("192.0.2.1", "RemoteStudent"), "A remote student must remain eligible for locking.");
        using var socket = new TcpListener(IPAddress.Loopback, 0);
        socket.Start();
        var port = ((IPEndPoint)socket.LocalEndpoint).Port;
        socket.Stop();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        using var client = new TeacherApiClient($"http://127.0.0.1:{port}", "smoke");
        foreach (var mode in new[] { InputLockVisualMode.FullscreenOverlay, InputLockVisualMode.DemonstrationBanner })
        {
            await MustRejectAsync(() => client.SetInputLockEnabledAsync(true, mode));
        }

        await MustRejectAsync(() => client.SetBrowserLockEnabledAsync(true));
        foreach (var input in new[] { true, false })
        {
            var contextTask = listener.GetContextAsync();
            var unlock = input ? client.SetInputLockEnabledAsync(false) : client.SetBrowserLockEnabledAsync(false);
            var context = await contextTask.WaitAsync(TimeSpan.FromSeconds(5));
            using var body = await JsonDocument.ParseAsync(context.Request.InputStream);
            Require(!body.RootElement.GetProperty("enabled").GetBoolean(), "Only unlocking requests may reach the local agent.");
            Require(context.Request.Url!.AbsolutePath == (input ? "/api/input-lock" : "/api/browser-lock"), "Unlock must retain the correct endpoint.");
            context.Response.StatusCode = 204;
            context.Response.Close();
            await unlock;
        }

        Console.WriteLine("PASS: local PC detection, rejected input/browser locks, allowed unlocking requests.");
    }

    private static async Task MustRejectAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (InvalidOperationException)
        {
            return;
        }

        throw new InvalidOperationException("A local locking request must be rejected before HTTP delivery.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
