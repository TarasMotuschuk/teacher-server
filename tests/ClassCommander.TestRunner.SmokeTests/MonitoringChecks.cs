using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Threading;
using ClassCommander.Testing.Core.Serialization;
using Teacher.Common.Contracts.Testing;
using TeacherClient.CrossPlatform.Dialogs;
using TeacherClient.CrossPlatform.Services;

namespace ClassCommander.TestRunner.SmokeTests;

internal static class MonitoringChecks
{
    public static void Run()
    {
        using var portFinder = new TcpListener(IPAddress.Loopback, 0);
        portFinder.Start();
        var port = ((IPEndPoint)portFinder.LocalEndpoint).Port;
        portFinder.Stop();
        using var server = new HttpListener();
        var url = $"http://127.0.0.1:{port}/";
        server.Prefixes.Add(url);
        server.Start();
        var resultReady = false;
        var requests = new List<string>();
        var service = Task.Run(async () =>
        {
            try
            {
                while (server.IsListening)
                {
                    var context = await server.GetContextAsync();
                    var path = context.Request.Url!.AbsolutePath;
                    lock (requests)
                    {
                        requests.Add(path);
                    }

                    var active = new AssignmentDto("active", "test", 1, "Current test",
                        new AssignmentAudienceDto(AudienceType.Class, null, null), new AssignmentAvailabilityDto(null, null),
                        new AttemptPolicyDto(1, null), new ResultPolicyDto(true, false, false), AssignmentStatus.Published);
                    object response = path.EndsWith("/assignments", StringComparison.Ordinal)
                        ? new PagedResponseDto<AssignmentDto>([active, active with { PublicId = "closed", Title = "Past test", Status = AssignmentStatus.Closed }], 2)
                        : path.EndsWith("/attempts", StringComparison.Ordinal)
                            ? new PagedResponseDto<AttemptListItemDto>([new AttemptListItemDto("attempt", new AttemptStudentDto(null, "Коваленко", "Олена", null, null, "PUPIL01"), AttemptStatus.Scored, DateTime.UtcNow, DateTime.UtcNow, 1, 1)], 1)
                            : new PagedResponseDto<ResultDto>(Volatile.Read(ref resultReady) ? [new ResultDto("attempt", 1, 1, 100, null, DateTime.UtcNow, [])] : [], Volatile.Read(ref resultReady) ? 1 : 0);
                    var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(response, response.GetType(), TestPlatformJson.Options));
                    context.Response.ContentType = "application/json";
                    await context.Response.OutputStream.WriteAsync(bytes);
                    context.Response.Close();
                }
            }
            catch (Exception) when (!server.IsListening)
            {
                // Listener shutdown ends the test server.
            }
        });

        var window = new TestingWindow();
        using var api = new TestPlatformApiClient(url);
        typeof(TestingWindow).GetField("_api", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, api);
        Pump((Task)Call(window, "RefreshAssignmentsAsync")!);
        var launch = window.FindControl<ComboBox>("LaunchAssignmentComboBox")!;
        var monitor = window.FindControl<ComboBox>("MonitorAssignmentComboBox")!;
        Check(launch.SelectedItem is not null && ReferenceEquals(launch.SelectedItem, monitor.SelectedItem), "Launch selection must also select a monitored assignment.");
        window.FindControl<TabControl>("MainTabControl")!.SelectedItem = window.FindControl<TabItem>("MonitorTabItem");
        WaitUntil(() => !IsRefreshing(window));
        Check(window.FindControl<DataGrid>("AttemptsGrid")!.ItemsSource.Cast<object>().Count() == 1, "Opening the monitor tab must fetch attempts without Open monitor.");
        Check(!window.FindControl<DataGrid>("ResultsGrid")!.ItemsSource.Cast<object>().Any(), "Incomplete results must start empty.");
        Volatile.Write(ref resultReady, true);
        var timer = (DispatcherTimer)typeof(TestingWindow).GetField("_monitorTimer", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
        timer.Start();
        WaitUntil(() => window.FindControl<DataGrid>("ResultsGrid")!.ItemsSource.Cast<object>().Any());
        timer.Stop();
        var row = window.FindControl<DataGrid>("ResultsGrid")!.ItemsSource.Cast<object>().Single();
        Check((string?)row.GetType().GetProperty("Student")!.GetValue(row) == "Коваленко Олена", "Scores must identify the student.");
        Check(monitor.ItemCount == 2 && launch.ItemCount == 1, "Closed tests remain available in monitoring only.");
        monitor.SelectedItem = monitor.ItemsSource!.Cast<object>().Single(item =>
            (string?)item.GetType().GetProperty("PublicId")!.GetValue(item) == "closed");
        WaitUntil(() => !IsRefreshing(window));
        lock (requests)
        {
            Check(requests.Contains("/api/tests/v1/assignments/closed/results"), "Changing the monitor selector must load the selected test.");
        }

        window.Close();
        server.Stop();
        Pump(service);
        Console.WriteLine("PASS: monitor selection, tab refresh, new results, student names, closed assignments.");
    }

    private static object? Call(TestingWindow window, string method)
        => typeof(TestingWindow).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);

    private static bool IsRefreshing(TestingWindow window)
        => (bool)typeof(TestingWindow).GetField("_monitorRefreshing", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;

    private static void Pump(Task task)
    {
        WaitUntil(() => task.IsCompleted);
        task.GetAwaiter().GetResult();
    }

    private static void WaitUntil(Func<bool> condition)
    {
        if (condition())
        {
            return;
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var check = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
        check.Tick += (_, _) =>
        {
            if (condition())
            {
                timeout.Cancel();
            }
        };
        check.Start();
        try
        {
            Dispatcher.UIThread.MainLoop(timeout.Token);
        }
        finally
        {
            check.Stop();
        }

        if (!condition())
        {
            throw new TimeoutException("Monitoring check timed out.");
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
