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
    private readonly RunnerSettingsStore _settingsStore;
    private readonly ObservableCollection<AssignmentListItem> _assignments = [];
    private readonly Dictionary<string, AttemptAnswerValueDto> _answers = new(StringComparer.Ordinal);

    private RunnerSettings _settings = new();
    private TestPlatformApiClient? _api;
    private AttemptStudentDto? _student;
    private StartAttemptResponse? _attempt;
    private List<QuestionDto> _questions = [];
    private int _questionIndex;
    private IAnswerEditor? _currentEditor;
    private readonly Dictionary<string, byte[]> _imageAssets = [];
    private bool _busy;

    public MainWindow()
        : this(new RunnerSettingsStore())
    {
    }

    internal MainWindow(RunnerSettingsStore settingsStore)
    {
        _settingsStore = settingsStore;
        InitializeComponent();
        AssignmentsListBox.ItemsSource = _assignments;
        _settings = _settingsStore.Load();
        ApplyLaunchOptionsToSettings();
        ResolveLanguage();
        ApplySettingsToForm();
        ApplyLocalization();
        ShowPanel(identity: true);
        Opened += InitializeStudentFlow;
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
        // A classroom PC is shared: never reuse the previous student's identity.
        SurnameTextBox.Text = string.Empty;
        NameTextBox.Text = string.Empty;
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
        ContinueButton.Content = string.IsNullOrWhiteSpace(RunnerLaunchOptions.Current.AssignmentPublicId)
            ? TestRunnerText.ContinueCommand : TestRunnerText.StartCommand;
        SurnameTextBox.Watermark = TestRunnerText.SurnameLabel;
        NameTextBox.Watermark = TestRunnerText.NameLabel;
        AssignmentsHeadingText.Text = TestRunnerText.AssignmentsHeading;
        RefreshAssignmentsButton.Content = TestRunnerText.RefreshCommand;
        BackToIdentityButton.Content = TestRunnerText.BackCommand;
        StartAssignmentButton.Content = TestRunnerText.StartCommand;
        PreviousQuestionButton.Content = TestRunnerText.PreviousCommand;
        NextQuestionButton.Content = TestRunnerText.NextCommand;
        SaveProgressButton.Content = TestRunnerText.SaveProgressCommand;
        ResultHeadingText.Text = TestRunnerText.ResultHeading;
        DoneButton.Content = TestRunnerText.DoneCommand;
        if (_attempt is null)
        {
            StatusTextBlock.Text = TestRunnerText.StatusReady;
        }

        LocalizeStudentFlow();
        RefreshQuestionChrome();
        RebuildCurrentEditor();
    }

    private async void ContinueButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await ContinueAsync();
    }

    private async Task TryStartLaunchedAssignmentAsync()
    {
        var assignmentId = RunnerLaunchOptions.Current.AssignmentPublicId;
        if (_busy || _api is null || _student is null || string.IsNullOrWhiteSpace(assignmentId))
        {
            return;
        }

        await StartSelectedAssignmentAsync(assignmentId);
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
        _student = null;
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
        if (_student is not null)
        {
            await TryStartLaunchedAssignmentAsync();
        }
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

        await LoadOverviewAsync(selected.AssignmentPublicId);
    }

    private async Task LoadImagesAsync(StartAttemptResponse response)
    {
        _imageAssets.Clear();
        var ids = response.TestDefinition.Groups.SelectMany(group => group.Questions)
            .Where(question => question.Type == QuestionType.ImagePoint)
            .SelectMany(question => question.Assets.Where(asset => asset.Role == "prompt"))
            .Select(asset => asset.AssetId).Distinct();
        foreach (var id in ids)
        {
            _imageAssets[id] = await _api!.GetImageAsync(response.AttemptPublicId, response.AttemptToken, id);
        }
    }

    private void BeginAttempt(StartAttemptResponse response)
    {
        ReleaseTestSession();
        StartTestSession();
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
        _timeExpired = false;
        AnswerHost.IsEnabled = true;
        FinishMenuItem.IsEnabled = true;
        ShowPanel(attempt: true);
        RefreshQuestionChrome();
        RebuildCurrentEditor();
        StartProgressClock();
        StatusTextBlock.Text = TestRunnerText.StatusReady;
    }

    private void PreviousQuestionButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_busy || _timeExpired || _questionIndex <= 0)
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
        if (_busy || _timeExpired || _questions.Count == 0)
        {
            return;
        }

        if (_questionIndex == _questions.Count - 1)
        {
            SubmitButton_OnClick(sender, e);
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
                new SaveAttemptProgressRequest(BuildAnswers(isFinal: false), BuildClientProgress(), ReplaceAnswers: true));
            StatusTextBlock.Text = TestRunnerText.ProgressSaved;
        });
    }

    private async void SubmitButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_busy || _api is null || _attempt is null)
        {
            return;
        }

        if (_sessionDialogOpen)
        {
            return;
        }

        CaptureCurrentAnswer();
        if (!_timeExpired && !await ConfirmAsync(TestRunnerText.ConfirmSubmit + Environment.NewLine
            + TestRunnerText.Unanswered(Math.Max(0, _questions.Count - _answers.Count))))
        {
            return;
        }

        await SubmitCurrentAsync();
    }

    private void DoneButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _attempt = null;
        _questions = [];
        _answers.Clear();
        (_currentEditor as IDisposable)?.Dispose();
        _currentEditor = null;
        AnswerHost.Child = null;
        BackToIdentityButton_OnClick(sender, e);
        ApplySettingsToForm();
    }

    private void ShowResult(SubmitAttemptResponse response)
    {
        ReleaseTestSession();
        PopulateResultSummary(response);
        var result = response.Result;
        var policy = response.ResultView;
        ResultScoreText.Text = policy.ShowScore
            ? TestRunnerText.ScoreLine(result.ScoreEarned, result.ScoreMax, result.Percent)
            : TestRunnerText.ScoreHidden;

        ResultDetailsText.Text = policy.ShowPerQuestionFeedback
            ? string.Join(
                Environment.NewLine,
                result.QuestionResults.Select((q, i) =>
                    policy.ShowScore
                        ? $"{i + 1}. {(q.IsCorrect ? "✓" : "✗")} {q.ScoreEarned:0.##}/{q.ScoreMax:0.##}"
                        : $"{i + 1}. {(q.IsCorrect ? "✓" : "✗")}"))
            : string.Empty;

        ShowPanel(result: true);
        StatusTextBlock.Text = TestRunnerText.StatusReady;
    }

    private void BindAssignments(IReadOnlyList<ActiveAssignmentDto> assignments)
    {
        _assignments.Clear();
        foreach (var item in assignments.Where(item =>
            string.IsNullOrWhiteSpace(RunnerLaunchOptions.Current.AssignmentPublicId)
            || item.AssignmentPublicId == RunnerLaunchOptions.Current.AssignmentPublicId))
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
            AnswerHintText.Text = string.Empty;
            PreviousQuestionButton.IsEnabled = false;
            NextQuestionButton.IsEnabled = false;
            return;
        }

        var question = _questions[_questionIndex];
        AnswerHintText.Text = TestRunnerText.AnswerHint(question.Type);
        QuestionProgressText.Text = TestRunnerText.QuestionProgress(_questionIndex + 1, _questions.Count);
        QuestionPromptText.Text = string.IsNullOrWhiteSpace(question.Description)
            ? question.Prompt
            : $"{question.Prompt}{Environment.NewLine}{Environment.NewLine}{question.Description}";
        PreviousQuestionButton.IsEnabled = _questionIndex > 0;
        NextQuestionButton.IsEnabled = !_timeExpired;
        NextQuestionButton.Content = _questionIndex == _questions.Count - 1 ? TestRunnerText.FinishCommand : TestRunnerText.NextCommand;
        SkipQuestionButton.IsEnabled = !_timeExpired;
    }

    private void RebuildCurrentEditor()
    {
        (_currentEditor as IDisposable)?.Dispose();
        AnswerHost.Child = null;
        _currentEditor = null;
        if (_questions.Count == 0)
        {
            return;
        }

        var question = _questions[_questionIndex];
        _answers.TryGetValue(question.Id, out var existing);
        var imageId = question.Assets.FirstOrDefault(asset => asset.Role == "prompt")?.AssetId;
        var image = imageId is not null && _imageAssets.TryGetValue(imageId, out var bytes) ? bytes : null;
        _currentEditor = AnswerEditorFactory.Create(question, existing, image);
        AnswerHost.Child = _currentEditor.Control;
        ApplyContentScale();
        AnswersScrollViewer.Offset = default;
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

    private void ShowPanel(bool identity = false, bool assignments = false, bool attempt = false, bool result = false, bool overview = false)
    {
        OverviewPanel.IsVisible = overview;
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
            StatusTextBlock.Text = ex.Message;
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
        await ShowSessionDialogAsync<object?>(dialog);
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
        await ShowSessionDialogAsync<object?>(dialog);
        return result;
    }

    protected override void OnClosed(EventArgs e)
    {
        _progressTimer.Stop();
        _progressTimer.Tick -= UpdateAttemptProgress;
        (_currentEditor as IDisposable)?.Dispose();
        ReleaseTestSession();
        _api?.Dispose();
        base.OnClosed(e);
    }
}

internal sealed record AssignmentListItem(
    string AssignmentPublicId,
    string Title,
    string Details);
