using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ClassCommander.Testing.Core.Serialization;
using ClassCommander.TestRunner.Services;
using Teacher.Common.Contracts.Testing;

namespace ClassCommander.TestRunner.SmokeTests;

internal static class StudentFlowChecks
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
        var starts = 0;
        var submissions = 0;
        var failSubmit = true;
        var output = Path.Combine(Path.GetTempPath(), "classcommander-student-flow");
        Directory.CreateDirectory(output);
        var question = new QuestionDto("choice", QuestionType.SingleChoice, "Виберіть правильний варіант / Choose the correct answer", null, 1, true, [], null,
            new SingleChoiceInteractionDto([new OptionDto("a", "Коротка відповідь / Short answer", 0), new OptionDto("b", new string('W', 100), 1)]),
            new ChoiceAnswerKeyDto(["a"]), null);
        var ordering = question with
        {
            Id = "order",
            Type = QuestionType.Ordering,
            Interaction = new OrderingInteractionDto([new OptionDto("a", "First", 0), new OptionDto("b", "Second", 1)]),
            AnswerKey = new OrderingAnswerKeyDto(["a", "b"]),
        };
        var definition = new TestDefinitionDto(1, "test", "test", 1, "Інформаційні системи / Information systems", "Опис тесту / Test description", "uk", null, [], [],
            new AuthorDto("Teacher", null), null, new TestSettingsDto(false, false, false, null, 1, "score"), [],
            [new TestGroupDto("group", "Group", null, 0, [question, ordering])]);
        var assignment = new AssignmentDto("chosen", "test", 1, definition.Title,
            new AssignmentAudienceDto(AudienceType.Class, null, null), new AssignmentAvailabilityDto(null, null),
            new AttemptPolicyDto(1, null), new ResultPolicyDto(true, false, true), AssignmentStatus.Published);
        var started = DateTime.UtcNow.AddMinutes(-2);
        var attempt = new StartAttemptResponse("attempt", AttemptStatus.InProgress, started, null, "token", assignment, definition, []);
        var result = new SubmitAttemptResponse("attempt", AttemptStatus.Scored, DateTime.UtcNow,
            new ResultDto("attempt", 1, 2, 50, 6, DateTime.UtcNow, [new QuestionResultDto("choice", true, 1, 1), new QuestionResultDto("order", false, 0, 1)]), assignment.ResultPolicy);
        var service = Task.Run(async () =>
        {
            try
            {
                while (server.IsListening)
                {
                    var context = await server.GetContextAsync();
                    var path = context.Request.Url!.AbsolutePath;
                    object response;
                    if (path.EndsWith("/overview", StringComparison.Ordinal))
                    {
                        response = new StudentTestOverviewDto("chosen", definition.Title, definition.Description, "Teacher", 2, 2, null, assignment.ResultPolicy);
                    }
                    else if (path.EndsWith("/resolve", StringComparison.Ordinal))
                    {
                        response = new ResolveStudentResponse(new AttemptStudentDto(null, "Коваленко", "Олена", null, null, null), [new ActiveAssignmentDto("chosen", assignment.Title, null, null)]);
                    }
                    else if (path.EndsWith("/attempts", StringComparison.Ordinal))
                    {
                        Interlocked.Increment(ref starts);
                        response = attempt;
                    }
                    else if (path.EndsWith("/submit", StringComparison.Ordinal))
                    {
                        Interlocked.Increment(ref submissions);
                        Check(context.Request.Headers["X-Attempt-Token"] == "token", "Submission must authenticate the attempt.");
                        var request = await JsonSerializer.DeserializeAsync<SubmitAttemptRequest>(context.Request.InputStream, TestPlatformJson.Options);
                        Check(request is { ReplaceAnswers: true }, "Final submission must send an authoritative answer snapshot.");
                        context.Response.StatusCode = Volatile.Read(ref failSubmit) ? 503 : 200;
                        response = result;
                    }
                    else
                    {
                        response = new SaveAttemptProgressResponse("attempt", AttemptStatus.InProgress, DateTime.UtcNow);
                    }

                    context.Response.ContentType = "application/json";
                    await JsonSerializer.SerializeAsync(context.Response.OutputStream, response, response.GetType(), TestPlatformJson.Options);
                    context.Response.Close();
                }
            }
            catch (Exception) when (!server.IsListening)
            {
                // Listener shutdown ends the fixture.
            }
        });

        foreach (var language in new[] { "uk", "en" })
        {
            RunnerLaunchOptions.Apply(["--server-url", url, "--assignment-id", "chosen", "--language", language]);
            var beforeStart = Volatile.Read(ref starts);
            var window = new MainWindow(new RunnerSettingsStore(Path.Combine(output, $"{language}.json"))) { Width = 800, Height = 600 };
            window.Show();
            WaitUntil(() => window.FindControl<Button>("OverviewContinueButton")!.IsEnabled);
            Check(window.FindControl<Border>("OverviewPanel")!.IsVisible && beforeStart == Volatile.Read(ref starts), "Reading the overview must not create an attempt.");
            Snapshot(window, Path.Combine(output, $"overview-{language}.png"));
            Click(window, "OverviewContinueButton");
            Check(window.FindControl<Border>("IdentityPanel")!.IsVisible, "The overview must lead to identity entry.");
            window.FindControl<TextBox>("SurnameTextBox")!.Text = "Коваленко";
            window.FindControl<TextBox>("NameTextBox")!.Text = "Олена";
            Click(window, "ContinueButton");
            WaitUntil(() => window.FindControl<Border>("AttemptPanel")!.IsVisible);
            var radio = ((StackPanel)window.FindControl<Border>("AnswerHost")!.Child!).Children.OfType<RadioButton>().First();
            Check(radio.Bounds.Width > 500, "The entire answer row must be a wide click target.");
            Snapshot(window, Path.Combine(output, $"question-{language}.png"));
            var point = radio.TranslatePoint(new Avalonia.Point(radio.Bounds.Width - 8, radio.Bounds.Height / 2), window)!.Value;
            window.MouseDown(point, Avalonia.Input.MouseButton.Left);
            window.MouseUp(point, Avalonia.Input.MouseButton.Left);
            Check(radio.IsChecked == true, $"Clicking the far end of an answer row must select it ({language}; {point}; {radio.Bounds}).");
            Click(window, "NextQuestionButton");
            Check(window.FindControl<Button>("NextQuestionButton")!.Content!.ToString()!.Contains(language == "uk" ? "Завершити" : "Finish", StringComparison.Ordinal), "The last question must offer Finish.");
            Call(window, "UpdateProgressDisplay");
            Check(window.FindControl<ProgressBar>("AttemptProgressBar")!.Value == 50, "Opening an ordering question must not count as an answer.");
            Click(window, "PreviousQuestionButton");
            radio = ((StackPanel)window.FindControl<Border>("AnswerHost")!.Child!).Children.OfType<RadioButton>().First();
            Check(radio.IsChecked == true, "Navigating back must preserve the answer.");
            Call(window, "SetContentScale", 1.6d);
            window.Width = 640;
            window.Height = 480;
            Dispatcher.UIThread.RunJobs();
            Snapshot(window, Path.Combine(output, $"question-small-{language}.png"));
            var next = window.FindControl<Button>("NextQuestionButton")!;
            var nextPosition = next.TranslatePoint(default, window)!.Value;
            Check(nextPosition.Y + next.Bounds.Height <= window.ClientSize.Height, "Navigation must stay visible on a small display.");
            Volatile.Write(ref failSubmit, true);
            Pump((Task)Call(window, "SubmitCurrentAsync")!);
            Check(window.FindControl<Border>("AttemptPanel")!.IsVisible && !window.FindControl<Border>("ResultPanel")!.IsVisible, "A failed submission must not claim successful delivery.");
            Check(window.FindControl<Border>("AnswerHost")!.IsEnabled, "Failed submissions must keep answers editable.");
            Volatile.Write(ref failSubmit, false);
            Pump((Task)Call(window, "SubmitCurrentAsync")!);
            Check(window.FindControl<Border>("ResultPanel")!.IsVisible && window.FindControl<Border>("AnswerHost")!.Child is null, "Confirmed submission must replace the question with a result screen.");
            Check(window.FindControl<TextBlock>("ResultGradeText")!.IsVisible && window.FindControl<TextBlock>("ResultGradeText")!.Text!.Contains('6'), "A supplied grade must be shown.");
            Snapshot(window, Path.Combine(output, $"result-small-{language}.png"));
            window.Width = 800;
            window.Height = 600;
            Snapshot(window, Path.Combine(output, $"result-{language}.png"));
            Call(window, "ShowResult", result with { ResultView = new ResultPolicyDto(false, false, true) });
            Check(!window.FindControl<TextBlock>("ResultDetailsText")!.Text!.Contains('/'), "Per-question feedback must not reveal hidden point scores.");
            Call(window, "ShowResult", result with { ResultView = new ResultPolicyDto(false, false, false) });
            Check(!window.FindControl<TextBlock>("ResultGradeText")!.IsVisible && string.IsNullOrEmpty(window.FindControl<TextBlock>("ResultDetailsText")!.Text), "Hidden scores and feedback must not leak on the result screen.");
            Check(!window.FindControl<TextBlock>("ResultScoreText")!.Text!.Contains("50", StringComparison.Ordinal), "Hidden percentages must not leak.");
            Click(window, "DoneButton");
            Check(string.IsNullOrEmpty(window.FindControl<TextBox>("SurnameTextBox")!.Text), "Completion must clear the previous student's identity.");
            window.Close();
        }

        RunnerLaunchOptions.Apply(["--language", "en"]);
        var timed = new MainWindow(new RunnerSettingsStore(Path.Combine(output, "timed.json")));
        timed.Show();
        typeof(MainWindow).GetField("_api", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(timed, new TestPlatformApiClient(url));
        Call(timed, "BeginAttempt", attempt with { Assignment = assignment with { AttemptPolicy = new AttemptPolicyDto(1, 1) } });
        var submittedBefore = Volatile.Read(ref submissions);
        Call(timed, "UpdateAttemptProgress", new object(), EventArgs.Empty);
        WaitUntil(() => timed.FindControl<Border>("ResultPanel")!.IsVisible);
        Check(Volatile.Read(ref submissions) > submittedBefore, "An elapsed deadline must submit automatically using the original attempt start time.");
        timed.Close();
        server.Stop();
        Pump(service);
        Console.WriteLine($"PASS: student overview before attempt, identity, full answer rows, navigation, zoom, progress, failed-submit retry, policy-aware results, deadline and identity cleanup. Screenshots: {output}");
    }

    private static void Snapshot(MainWindow window, string path)
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(2);
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame()!.Save(path);
    }

    private static object? Call(MainWindow window, string method, params object[] args)
        => typeof(MainWindow).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, args);

    private static void Click(MainWindow window, string name)
    {
        window.FindControl<Button>(name)!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static void Pump(Task task)
    {
        WaitUntil(() => task.IsCompleted);
        task.GetAwaiter().GetResult();
    }

    private static void WaitUntil(Func<bool> condition)
    {
        var end = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < end)
        {
            Dispatcher.UIThread.RunJobs();
            Thread.Sleep(10);
        }

        Dispatcher.UIThread.RunJobs();
        Check(condition(), "Student flow operation timed out.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
