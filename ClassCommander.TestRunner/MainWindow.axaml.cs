using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ClassCommander.TestRunner.Localization;
using ClassCommander.TestRunner.Services;
using Teacher.Common.Contracts.Testing;
using Teacher.Common.Localization;

namespace ClassCommander.TestRunner;

public partial class MainWindow : Window
{
    private readonly RunnerSettingsStore _settingsStore = new();
    private readonly ObservableCollection<AssignmentListItem> _assignments = [];
    private readonly Dictionary<string, AttemptAnswerValueDto> _answers = new(StringComparer.Ordinal);

    private RunnerSettings _settings = new();
    private TestPlatformApiClient? _api;
    private AttemptStudentDto? _student;
    private StartAttemptResponse? _attempt;
    private List<QuestionDto> _questions = [];
    private int _questionIndex;
    private IAnswerEditor? _currentEditor;
    private bool _busy;

    public MainWindow()
    {
        InitializeComponent();
        AssignmentsListBox.ItemsSource = _assignments;
        _settings = _settingsStore.Load();
        ApplyLaunchOptionsToSettings();
        ResolveLanguage();
        ApplySettingsToForm();
        ApplyLocalization();
        ShowPanel(identity: true);
        if (RunnerLaunchOptions.Current.AutoContinue)
        {
            Opened += async (_, _) => await TryAutoContinueAsync();
        }
    }

    private void ResolveLanguage()
    {
        var launchLanguage = RunnerLaunchOptions.Current.Language;
        TestRunnerText.Language = !string.IsNullOrWhiteSpace(launchLanguage)
            ? UiLanguageExtensions.Parse(launchLanguage)
            : ClassCommanderUiSettings.LoadLanguage();
    }

    private void ApplyLaunchOptionsToSettings()
    {
        var launch = RunnerLaunchOptions.Current;
        if (!string.IsNullOrWhiteSpace(launch.ServerUrl))
        {
            _settings.ServerUrl = launch.ServerUrl.Trim().TrimEnd('/');
        }

        if (!string.IsNullOrWhiteSpace(launch.Surname))
        {
            _settings.Surname = launch.Surname.Trim();
        }

        if (!string.IsNullOrWhiteSpace(launch.Name))
        {
            _settings.Name = launch.Name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(launch.ClassName))
        {
            _settings.ClassName = launch.ClassName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(launch.DeviceId))
        {
            _settings.DeviceId = launch.DeviceId.Trim();
        }

        if (!string.IsNullOrWhiteSpace(launch.ServerUrl)
            || !string.IsNullOrWhiteSpace(launch.ClassName)
            || !string.IsNullOrWhiteSpace(launch.DeviceId))
        {
            _settingsStore.Save(_settings);
        }
    }

    private void ApplySettingsToForm()
    {
        SurnameTextBox.Text = _settings.Surname;
        NameTextBox.Text = _settings.Name;
        ClassTextBox.Text = _settings.ClassName;
        DeviceTextBox.Text = _settings.DeviceId;
    }

    private void PersistIdentitySettings()
    {
        _settings.Surname = SurnameTextBox.Text?.Trim() ?? string.Empty;
        _settings.Name = NameTextBox.Text?.Trim() ?? string.Empty;
        _settings.ClassName = ClassTextBox.Text?.Trim() ?? string.Empty;
        _settings.DeviceId = DeviceTextBox.Text?.Trim() ?? string.Empty;
        _settings.Language = TestRunnerText.Language.ToCode();
        _settingsStore.Save(_settings);
    }

    private void ApplyLocalization()
    {
        Title = TestRunnerText.WindowTitle;
        IdentityHeadingText.Text = TestRunnerText.IdentityHeading;
        TeacherLaunchHintText.Text = TestRunnerText.TeacherLaunchHint;
        SurnameLabelText.Text = TestRunnerText.SurnameLabel;
        NameLabelText.Text = TestRunnerText.NameLabel;
        ClassLabelText.Text = TestRunnerText.ClassLabel;
        DeviceLabelText.Text = TestRunnerText.DeviceLabel;
        ContinueButton.Content = TestRunnerText.ContinueCommand;
        AssignmentsHeadingText.Text = TestRunnerText.AssignmentsHeading;
        RefreshAssignmentsButton.Content = TestRunnerText.RefreshCommand;
        BackToIdentityButton.Content = TestRunnerText.BackCommand;
        StartAssignmentButton.Content = TestRunnerText.StartCommand;
        PreviousQuestionButton.Content = TestRunnerText.PreviousCommand;
        NextQuestionButton.Content = TestRunnerText.NextCommand;
        SaveProgressButton.Content = TestRunnerText.SaveProgressCommand;
        SubmitButton.Content = TestRunnerText.SubmitCommand;
        ResultHeadingText.Text = TestRunnerText.ResultHeading;
        DoneButton.Content = TestRunnerText.DoneCommand;
        if (_attempt is null)
        {
            StatusTextBlock.Text = TestRunnerText.StatusReady;
        }

        RefreshQuestionChrome();
        RebuildCurrentEditor();
    }

    private async void ContinueButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => await ContinueAsync();

    private async Task TryAutoContinueAsync()
    {
        if (_busy)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(_settings.ServerUrl)
            || string.IsNullOrWhiteSpace(_settings.Surname)
            || string.IsNullOrWhiteSpace(_settings.Name))
        {
            return;
        }

        await ContinueAsync();
    }

    private async Task ContinueAsync()
    {
        if (_busy)
        {
            return;
        }

        var serverUrl = _settings.ServerUrl?.Trim() ?? string.Empty;
        var surname = SurnameTextBox.Text?.Trim() ?? string.Empty;
        var name = NameTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(serverUrl))
        {
            await ShowErrorAsync(TestRunnerText.RequiredServer);
            return;
        }

        if (string.IsNullOrWhiteSpace(surname) || string.IsNullOrWhiteSpace(name))
        {
            await ShowErrorAsync(TestRunnerText.RequiredFields);
            return;
        }

        PersistIdentitySettings();
        await RunBusyAsync(TestRunnerText.Connecting, async () =>
        {
            _api?.Dispose();
            _api = new TestPlatformApiClient(serverUrl);
            var response = await _api.ResolveAsync(new ResolveStudentRequest(
                surname,
                name,
                Middlename: null,
                string.IsNullOrWhiteSpace(ClassTextBox.Text) ? null : ClassTextBox.Text.Trim(),
                string.IsNullOrWhiteSpace(DeviceTextBox.Text) ? null : DeviceTextBox.Text.Trim()));

            _student = response.Student;
            BindAssignments(response.ActiveAssignments);
            ShowPanel(assignments: true);
            StatusTextBlock.Text = response.ActiveAssignments.Count == 0
                ? TestRunnerText.NoAssignments
                : TestRunnerText.StatusReady;
        });
    }

    private async void RefreshAssignmentsButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_api is null || _student is null || _busy)
        {
            return;
        }

        await RunBusyAsync(TestRunnerText.Connecting, async () =>
        {
            var response = await _api.ResolveAsync(new ResolveStudentRequest(
                _student.Surname,
                _student.Name,
                _student.Middlename,
                _student.ClassName,
                _student.DeviceId));
            _student = response.Student;
            BindAssignments(response.ActiveAssignments);
            StatusTextBlock.Text = response.ActiveAssignments.Count == 0
                ? TestRunnerText.NoAssignments
                : TestRunnerText.StatusReady;
        });
    }

    private void BackToIdentityButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_busy)
        {
            return;
        }

        _api?.Dispose();
        _api = null;
        _student = null;
        _assignments.Clear();
        ShowPanel(identity: true);
        StatusTextBlock.Text = TestRunnerText.StatusReady;
    }

    private async void StartAssignmentButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_busy || _api is null || _student is null)
        {
            return;
        }

        if (AssignmentsListBox.SelectedItem is not AssignmentListItem selected)
        {
            return;
        }

        await RunBusyAsync(TestRunnerText.LoadingTest, async () =>
        {
            var response = await _api.StartAttemptAsync(new StartAttemptRequest(
                selected.AssignmentPublicId,
                _student));

            BeginAttempt(response);
        });
    }

    private void BeginAttempt(StartAttemptResponse response)
    {
        _attempt = response;
        _answers.Clear();
        foreach (var saved in response.SavedAnswers)
        {
            _answers[saved.QuestionId] = saved.Value;
        }

        _questions = response.TestDefinition.Groups
            .OrderBy(g => g.Order)
            .SelectMany(g => g.Questions)
            .ToList();

        _questionIndex = 0;
        if (response.SavedAnswers.Count > 0)
        {
            var lastId = response.SavedAnswers[^1].QuestionId;
            var idx = _questions.FindIndex(q => q.Id == lastId);
            if (idx >= 0)
            {
                _questionIndex = idx;
            }
        }

        AttemptTitleText.Text = response.Assignment.Title;
        ShowPanel(attempt: true);
        RefreshQuestionChrome();
        RebuildCurrentEditor();
        StatusTextBlock.Text = TestRunnerText.StatusReady;
    }

    private void PreviousQuestionButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_busy || _questionIndex <= 0)
        {
            return;
        }

        CaptureCurrentAnswer();
        _questionIndex--;
        RefreshQuestionChrome();
        RebuildCurrentEditor();
    }

    private void NextQuestionButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_busy || _questionIndex >= _questions.Count - 1)
        {
            return;
        }

        CaptureCurrentAnswer();
        _questionIndex++;
        RefreshQuestionChrome();
        RebuildCurrentEditor();
    }

    private async void SaveProgressButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_busy || _api is null || _attempt is null)
        {
            return;
        }

        CaptureCurrentAnswer();
        await RunBusyAsync(TestRunnerText.Connecting, async () =>
        {
            await _api.SaveProgressAsync(
                _attempt.AttemptPublicId,
                _attempt.AttemptToken,
                new SaveAttemptProgressRequest(BuildAnswers(isFinal: false), BuildClientProgress()));
            StatusTextBlock.Text = TestRunnerText.ProgressSaved;
        });
    }

    private async void SubmitButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_busy || _api is null || _attempt is null)
        {
            return;
        }

        if (!await ConfirmAsync(TestRunnerText.ConfirmSubmit))
        {
            return;
        }

        CaptureCurrentAnswer();
        await RunBusyAsync(TestRunnerText.Connecting, async () =>
        {
            var response = await _api.SubmitAsync(
                _attempt.AttemptPublicId,
                _attempt.AttemptToken,
                new SubmitAttemptRequest(BuildAnswers(isFinal: true)));

            ShowResult(response);
        });
    }

    private void DoneButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _attempt = null;
        _questions = [];
        _answers.Clear();
        _currentEditor = null;
        AnswerHost.Child = null;
        ShowPanel(assignments: true);
        StatusTextBlock.Text = TestRunnerText.StatusReady;
    }

    private void ShowResult(SubmitAttemptResponse response)
    {
        var result = response.Result;
        var policy = response.ResultView;
        ResultScoreText.Text = policy.ShowScore
            ? TestRunnerText.ScoreLine(result.ScoreEarned, result.ScoreMax, result.Percent)
            : (_attempt?.Assignment.Title ?? TestRunnerText.ResultHeading);

        ResultDetailsText.Text = policy.ShowPerQuestionFeedback
            ? string.Join(
                Environment.NewLine,
                result.QuestionResults.Select((q, i) =>
                    $"{i + 1}. {(q.IsCorrect ? "✓" : "✗")} {q.ScoreEarned:0.##}/{q.ScoreMax:0.##}"))
            : string.Empty;

        ShowPanel(result: true);
        StatusTextBlock.Text = TestRunnerText.StatusReady;
    }

    private void BindAssignments(IReadOnlyList<ActiveAssignmentDto> assignments)
    {
        _assignments.Clear();
        foreach (var item in assignments)
        {
            var window = FormatAvailability(item.StartUtc, item.EndUtc);
            _assignments.Add(new AssignmentListItem(
                item.AssignmentPublicId,
                item.Title,
                window));
        }
    }

    private static string FormatAvailability(DateTime? startUtc, DateTime? endUtc)
    {
        if (startUtc is null && endUtc is null)
        {
            return string.Empty;
        }

        var start = startUtc?.ToLocalTime().ToString("g", CultureInfo.CurrentCulture) ?? "—";
        var end = endUtc?.ToLocalTime().ToString("g", CultureInfo.CurrentCulture) ?? "—";
        return $"{start} → {end}";
    }

    private void RefreshQuestionChrome()
    {
        if (_questions.Count == 0)
        {
            QuestionProgressText.Text = string.Empty;
            QuestionPromptText.Text = string.Empty;
            PreviousQuestionButton.IsEnabled = false;
            NextQuestionButton.IsEnabled = false;
            return;
        }

        var question = _questions[_questionIndex];
        QuestionProgressText.Text = TestRunnerText.QuestionProgress(_questionIndex + 1, _questions.Count);
        QuestionPromptText.Text = string.IsNullOrWhiteSpace(question.Description)
            ? question.Prompt
            : $"{question.Prompt}{Environment.NewLine}{Environment.NewLine}{question.Description}";
        PreviousQuestionButton.IsEnabled = _questionIndex > 0;
        NextQuestionButton.IsEnabled = _questionIndex < _questions.Count - 1;
    }

    private void RebuildCurrentEditor()
    {
        AnswerHost.Child = null;
        _currentEditor = null;
        if (_questions.Count == 0)
        {
            return;
        }

        var question = _questions[_questionIndex];
        _answers.TryGetValue(question.Id, out var existing);
        _currentEditor = AnswerEditorFactory.Create(question, existing);
        AnswerHost.Child = _currentEditor.Control;
    }

    private void CaptureCurrentAnswer()
    {
        if (_currentEditor is null || _questions.Count == 0)
        {
            return;
        }

        var question = _questions[_questionIndex];
        var value = _currentEditor.Collect();
        if (value is null)
        {
            _answers.Remove(question.Id);
            return;
        }

        _answers[question.Id] = value;
    }

    private IReadOnlyList<AttemptAnswerDto> BuildAnswers(bool isFinal)
    {
        var now = DateTime.UtcNow;
        return _questions
            .Where(q => _answers.ContainsKey(q.Id))
            .Select(q => new AttemptAnswerDto(q.Id, q.Type, _answers[q.Id], isFinal, now))
            .ToList();
    }

    private ClientProgressDto BuildClientProgress()
    {
        var currentId = _questions.Count == 0 ? null : _questions[_questionIndex].Id;
        return new ClientProgressDto(_answers.Count, _questions.Count, currentId);
    }

    private void ShowPanel(bool identity = false, bool assignments = false, bool attempt = false, bool result = false)
    {
        IdentityPanel.IsVisible = identity;
        AssignmentsPanel.IsVisible = assignments;
        AttemptPanel.IsVisible = attempt;
        ResultPanel.IsVisible = result;
    }

    private async Task RunBusyAsync(string status, Func<Task> action)
    {
        _busy = true;
        StatusTextBlock.Text = status;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex.Message);
            StatusTextBlock.Text = TestRunnerText.StatusReady;
        }
        finally
        {
            _busy = false;
        }
    }

    private async Task ShowErrorAsync(string message)
    {
        var dialog = new Window
        {
            Title = TestRunnerText.ErrorTitle,
            Width = 480,
            Height = 220,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new TextBlock
            {
                Text = message,
                Margin = new Thickness(20),
                TextWrapping = TextWrapping.Wrap,
            },
        };
        await dialog.ShowDialog(this);
    }

    private async Task<bool> ConfirmAsync(string message)
    {
        var result = false;
        var yes = new Button { Content = TestRunnerText.Yes, MinWidth = 80 };
        var no = new Button { Content = TestRunnerText.No, MinWidth = 80 };
        var dialog = new Window
        {
            Title = TestRunnerText.SubmitCommand,
            Width = 480,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new StackPanel
            {
                Margin = new Thickness(20),
                Spacing = 16,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 8,
                        Children = { yes, no },
                    },
                },
            },
        };

        yes.Click += (_, _) =>
        {
            result = true;
            dialog.Close();
        };
        no.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(this);
        return result;
    }

    protected override void OnClosed(EventArgs e)
    {
        _api?.Dispose();
        base.OnClosed(e);
    }
}

internal sealed record AssignmentListItem(
    string AssignmentPublicId,
    string Title,
    string Details);
