using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ClassCommander.TestRunner;
using ClassCommander.TestRunner.Services;
using Teacher.Common.Contracts.Testing;

var output = Path.Combine(Path.GetTempPath(), "classcommander-testing-ux-smoke");
Directory.CreateDirectory(output);
AppBuilder.Configure<App>().UseSkia()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .SetupWithoutStarting();

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static object? Invoke(MainWindow window, string method, params object[] args)
    => typeof(MainWindow).GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, args);

foreach (var language in new[] { "uk", "en" })
{
    // A previous student and legacy auto-start arguments must not bypass sign-in.
    var settings = new RunnerSettingsStore(Path.Combine(output, $"settings-{language}.json"));
    settings.Save(new RunnerSettings { Surname = "Previous", Name = "Student" });
    RunnerLaunchOptions.Apply(["--assignment-id", "chosen", "--surname", "PC01", "--name", "WindowsUser", "--auto-continue", "--language", language]);
    var window = new MainWindow(settings) { Width = 800, Height = 600 };
    window.Show();
    Dispatcher.UIThread.RunJobs();
    Require(window.ActualThemeVariant == ThemeVariant.Light, "Fixed white panels require the light theme.");
    Require(window.FindControl<Border>("IdentityPanel")!.IsVisible, "Sign-in must stay visible.");
    Require(string.IsNullOrEmpty(window.FindControl<TextBox>("SurnameTextBox")!.Text), "Do not reuse a surname.");
    Require(string.IsNullOrEmpty(window.FindControl<TextBox>("NameTextBox")!.Text), "Do not reuse a first name.");
    Require(!window.FindControl<TextBox>("DeviceTextBox")!.IsVisible, "Device details must not distract from identity.");
    window.CaptureRenderedFrame()!.Save(Path.Combine(output, $"identity-{language}.png"));

    Invoke(window, "BindAssignments", new List<ActiveAssignmentDto>
    {
        new("other", "Other test", null, null),
        new("chosen", "Chosen test", null, null),
    });
    Require(window.FindControl<ListBox>("AssignmentsListBox")!.ItemCount == 1, "A teacher launch must not offer other assignments.");
    window.Close();
}

RunnerLaunchOptions.Apply(["--language", "uk"]);
var preview = new MainWindow(new RunnerSettingsStore(Path.Combine(output, "preview.json"))) { Width = 800, Height = 600 };
preview.Show();
var question = new QuestionDto("question", QuestionType.TrueFalseGroup, "Позначте правильні твердження", null, 1, true, [], null,
    new TrueFalseGroupInteractionDto([new StatementDto("one", "Київ — столиця України", 0), new StatementDto("two", "2 + 2 = 5", 1)]),
    new TrueFalseGroupAnswerKeyDto([]), null);
var definition = new TestDefinitionDto(1, "test", "test", 1, "Перевірка інтерфейсу", null, "uk", null, [], [], null, null,
    new TestSettingsDto(false, false, false, null, 1, "score"), [], [new TestGroupDto("group", string.Empty, null, 0, [question])]);
var assignment = new AssignmentDto("chosen", "test", 1, "Перевірка інтерфейсу", new AssignmentAudienceDto(AudienceType.Class, null, null),
    new AssignmentAvailabilityDto(null, null), new AttemptPolicyDto(1, null), new ResultPolicyDto(true, false, false), AssignmentStatus.Published);
Invoke(preview, "BeginAttempt", new StartAttemptResponse("attempt", AttemptStatus.InProgress, DateTime.UtcNow, null, "token", assignment, definition, []));
Require(preview.FindControl<TextBlock>("AnswerHintText")!.Text!.Contains("Так", StringComparison.Ordinal), "True/false questions need a localized answer hint.");
preview.CaptureRenderedFrame()!.Save(Path.Combine(output, "question-uk.png"));
preview.Width = 640;
preview.Height = 480;
Dispatcher.UIThread.RunJobs();
preview.CaptureRenderedFrame()!.Save(Path.Combine(output, "question-small-uk.png"));
preview.Close();

// Headless native Windows hooks must not capture the machine running the tests.
if (!OperatingSystem.IsWindows())
{
    RunnerLaunchOptions.Apply(["--exit-code-hash", Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("123456")))]);
    var window = new MainWindow(new RunnerSettingsStore(Path.Combine(output, "session.json")));
    window.Show();
    Invoke(window, "StartTestSession");
    Require(window.Topmost && window.WindowState == WindowState.FullScreen, "Protected attempts must be fullscreen.");
    Require(window.FindControl<Border>("SessionBanner")!.IsVisible, "Restriction must be visible to the student.");
    window.Close();
    Dispatcher.UIThread.RunJobs();
    Require(window.IsVisible, "Closing a protected test must be cancelled.");
    var dialog = window.OwnedWindows.Single();
    var code = dialog.GetVisualDescendants().OfType<TextBox>().Single();
    var exit = dialog.GetVisualDescendants().OfType<Button>().Last();
    code.Text = "000000";
    exit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Dispatcher.UIThread.RunJobs();
    Require(dialog.IsVisible && window.Topmost, "An incorrect PIN must not unlock the test.");
    code.Text = "123456";
    exit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    Dispatcher.UIThread.RunJobs();
    Require(!window.IsVisible && !window.Topmost, "A correct PIN must release the session and close.");

    var completed = new MainWindow(new RunnerSettingsStore(Path.Combine(output, "completed.json")));
    completed.Show();
    Invoke(completed, "StartTestSession");
    Invoke(completed, "ShowResult", new SubmitAttemptResponse("attempt", AttemptStatus.Scored, DateTime.UtcNow,
        new ResultDto("attempt", 1, 1, 100, null, DateTime.UtcNow, []), new ResultPolicyDto(true, false, false)));
    Require(!completed.Topmost && completed.WindowState == WindowState.Normal, "Submitting must release fullscreen restrictions.");
    completed.Close();
}

var teacherAssembly = typeof(TeacherClient.CrossPlatform.DiscoveredAgentRow).Assembly;
var recipients = new[] { "PUPIL01", "PUPIL02" }.Select((name, index) =>
    TeacherClient.CrossPlatform.DiscoveredAgentRow.FromKnownEntry(new TeacherClient.CrossPlatform.Models.KnownAgentEntry
    {
        AgentId = name,
        MachineName = name,
        RespondingAddress = $"192.168.1.{index + 10}",
    })).ToList();
var owner = new Window();
owner.Show();
var choose = (Task<IReadOnlyList<TeacherClient.CrossPlatform.DiscoveredAgentRow>?>)teacherAssembly
    .GetType("TeacherClient.CrossPlatform.Dialogs.TestRecipientsDialog")!.GetMethod("ShowAsync")!
    .Invoke(null, [owner, "Тест з інформатики", recipients, Array.Empty<TeacherClient.CrossPlatform.DiscoveredAgentRow>()])!;
Dispatcher.UIThread.RunJobs();
var picker = owner.OwnedWindows.Single();
var start = picker.GetVisualDescendants().OfType<Button>().Last(button => button is not CheckBox);
Require(!start.IsEnabled, "Launching without selected recipients must be disabled.");
picker.GetVisualDescendants().OfType<CheckBox>().Last().IsChecked = true;
Require(start.IsEnabled, "Selecting one recipient must enable launch.");
picker.CaptureRenderedFrame()!.Save(Path.Combine(output, "teacher-recipients.png"));
start.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
Dispatcher.UIThread.RunJobs();
Require(
    choose.IsCompletedSuccessfully && choose.Result is { Count: 1 } && choose.Result[0].AgentId == "PUPIL02",
    "The launch must return only the explicitly selected PC.");
owner.Close();
var script = (string)teacherAssembly.GetType("TeacherClient.CrossPlatform.Services.TestClassroomLaunchHelper")!
    .GetMethod("BuildLaunchScript")!.Invoke(null, ["http://192.168.1.2:5050", "chosen", recipients[0], new string('A', 64)])!;
Require(
    !script.Contains("--auto-continue", StringComparison.Ordinal) && !script.Contains("--surname", StringComparison.Ordinal),
    "Teacher launch must not inject machine identity or skip sign-in.");
Require(script.Contains("--exit-code-hash", StringComparison.Ordinal), "Teacher launches must include the exit-code verifier.");

ClassCommander.TestRunner.SmokeTests.MonitoringChecks.Run();
ClassCommander.TestRunner.SmokeTests.ImagePointChecks.Run();
ClassCommander.TestRunner.SmokeTests.LocalComputerChecks.Run();
ClassCommander.TestRunner.SmokeTests.EditorWorkspaceChecks.Run();
ClassCommander.TestRunner.SmokeTests.OrderingEditorChecks.Run();

Console.WriteLine($"PASS: sign-in, localization, theme, assignment isolation, session cleanup. Screenshots: {output}");
