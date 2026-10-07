using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Teacher.Common.Contracts;
using TeacherClient.CrossPlatform;
using TeacherClient.CrossPlatform.Dialogs;
using TeacherClient.CrossPlatform.Models;
using TeacherClient.CrossPlatform.Services;

namespace ClassCommander.TestRunner.SmokeTests;

internal static class RemoteCommandChecks
{
    public static void Run()
    {
        Application.Current!.Styles.Add(new StyleInclude(new Uri("avares://TeacherClient.Avalonia/"))
        { Source = new Uri("avares://Avalonia.Controls.DataGrid/Themes/Fluent.xaml") });
        Task.Run(CheckApiAsync).GetAwaiter().GetResult();
        using var teacher = new TeacherClient.CrossPlatform.MainWindow();
        var offline = DiscoveredAgentRow.FromKnownEntry(new KnownAgentEntry { AgentId = "offline", MachineName = "Offline" });
        var onlineText = typeof(TeacherClient.CrossPlatform.MainWindow).Assembly.GetType("TeacherClient.CrossPlatform.Localization.CrossPlatformText")!
            .GetProperty("Online")!.GetValue(null)!.ToString()!;
        var selected = offline with { AgentId = "selected", MachineName = "Selected", Status = onlineText };
        var focused = offline with { AgentId = "focused", MachineName = "Focused", Status = onlineText };
        selected.GroupCommandSelected = true;
        offline.GroupCommandSelected = true;
        var rows = (List<DiscoveredAgentRow>)teacher.GetType().GetField("_allAgents", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(teacher)!;
        rows.Clear();
        rows.AddRange([offline, selected, focused]);
        teacher.FindControl<DataGrid>("AgentsGrid")!.SelectedItem = focused;
        var select = teacher.GetType().GetMethod("GetRemoteCommandTargets", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Check(((List<DiscoveredAgentRow>)select.Invoke(teacher, [true])!).SequenceEqual([selected]), "Commands target checked online PCs, not focused rows or checked offline PCs.");
        Check(((List<DiscoveredAgentRow>)select.Invoke(teacher, [false])!).Count == 2, "All command targets include every online PC.");
        teacher.Close();

        var entry = FrequentProgramEntry.Create("Example", "example.exe", RemoteCommandRunAs.CurrentUser);
        var window = new FrequentProgramsWindow([entry]);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var grid = window.FindControl<DataGrid>("ProgramsGrid")!;
        grid.SelectedIndex = 0;
        Click(window, "EditButton");
        var edit = window.OwnedWindows.Single();
        var fields = edit.GetVisualDescendants().OfType<TextBox>().ToArray();
        fields[0].Text = "Edited program";
        fields[1].Text = "updated.exe --argument";
        edit.GetVisualDescendants().OfType<ComboBox>().Single().SelectedIndex = 1;
        Dispatcher.UIThread.RunJobs();
        edit.CaptureRenderedFrame()!.Save(Path.Combine(Path.GetTempPath(), "classcommander-frequent-edit.png"));
        edit.GetVisualDescendants().OfType<Button>().Single(b => b.IsDefault).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
        var updated = grid.ItemsSource!.Cast<FrequentProgramEntry>().Single();
        Check(updated.Id == entry.Id && updated.DisplayName == "Edited program" && updated.CommandText == "updated.exe --argument"
            && updated.RunAs == RemoteCommandRunAs.Administrator, "Editing preserves identity and changes name, command and run mode.");
        var administratorLabel = typeof(TeacherClient.CrossPlatform.MainWindow).Assembly.GetType("TeacherClient.CrossPlatform.Localization.CrossPlatformText")!
            .GetProperty("RunAsAdministrator")!.GetValue(null)!.ToString();
        Check(updated.RunAsDisplay == administratorLabel, "The run mode uses the localized label.");
        window.CaptureRenderedFrame()!.Save(Path.Combine(Path.GetTempPath(), "classcommander-frequent-programs.png"));
        Click(window, "ClearButton");
        var confirmation = window.OwnedWindows.Single();
        Click(confirmation, "CancelButton");
        Check(grid.ItemsSource!.Cast<object>().Count() == 1, "Cancelling clear keeps the list.");
        Click(window, "ClearButton");
        Click(window.OwnedWindows.Single(), "OkButton");
        Check(!grid.ItemsSource!.Cast<object>().Any(), "Confirmed clear removes all entries.");
        Check(!window.FindControl<Button>("ClearButton")!.IsEnabled, "Empty lists cannot be cleared again.");
        window.Close();
        Console.WriteLine("PASS: checked online command targets, frequent-program editing/run mode and confirmed clear.");
    }

    private static async Task CheckApiAsync()
    {
        using var socket = new TcpListener(IPAddress.Loopback, 0);
        socket.Start();
        var port = ((IPEndPoint)socket.LocalEndpoint).Port;
        socket.Stop();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        listener.Start();
        using var client = new TeacherApiClient($"http://127.0.0.1:{port}", "smoke");
        foreach (var mode in new[] { RemoteCommandRunAs.CurrentUser, RemoteCommandRunAs.Administrator })
        {
            var requestTask = listener.GetContextAsync();
            var launch = client.ExecuteRemoteCommandAsync("echo Привіт", mode);
            var context = await requestTask.WaitAsync(TimeSpan.FromSeconds(5));
            using var body = await JsonDocument.ParseAsync(context.Request.InputStream);
            Check(context.Request.Url!.AbsolutePath == "/api/commands/run"
                && body.RootElement.GetProperty("script").GetString() == "echo Привіт"
                && body.RootElement.GetProperty("runAs").GetInt32() == (int)mode, "Remote command API preserves Unicode scripts and run mode.");
            context.Response.StatusCode = mode == RemoteCommandRunAs.CurrentUser ? 200 : 400;
            context.Response.ContentType = "application/json";
            var bytes = System.Text.Encoding.UTF8.GetBytes(mode == RemoteCommandRunAs.CurrentUser ? "{}" : "{\"error\":\"No signed-in interactive user was found.\"}");
            await context.Response.OutputStream.WriteAsync(bytes);
            context.Response.Close();
            try
            {
                await launch;
                Check(mode == RemoteCommandRunAs.CurrentUser, "Agent errors must not look like successful launches.");
            }
            catch (HttpRequestException ex) when (mode == RemoteCommandRunAs.Administrator)
            {
                Check(ex.Message == "No signed-in interactive user was found.", "Show the agent's launch error.");
            }
        }
    }

    private static void Click(Window window, string name)
    {
        window.FindControl<Button>(name)!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
