using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ClassCommander.TestRunner.Localization;
using ClassCommander.TestRunner.Services;
using Teacher.Common.Contracts.Testing;

namespace ClassCommander.TestRunner;

public partial class MainWindow
{
    private readonly DispatcherTimer _progressTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private StudentTestOverviewDto? _overview;
    private string? _overviewAssignmentId;
    private double _contentScale = 1;
    private bool _timeExpired;
    private DateTime _retrySubmitAfterUtc;

    private async void InitializeStudentFlow(object? sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(RunnerLaunchOptions.Current.AssignmentPublicId)
            && !string.IsNullOrWhiteSpace(_settings.ServerUrl))
        {
            await LoadOverviewAsync(RunnerLaunchOptions.Current.AssignmentPublicId);
        }
        else
        {
            SurnameTextBox.Focus();
        }
    }

    private async Task LoadOverviewAsync(string assignmentId)
    {
        if (_busy)
        {
            return;
        }

        _overviewAssignmentId = assignmentId;
        _overview = null;
        ShowPanel(overview: true);
        OverviewContinueButton.IsEnabled = false;
        OverviewRetryButton.IsVisible = false;
        OverviewTitleText.Text = TestRunnerText.LoadingTest;
        OverviewDescriptionText.Text = string.Empty;
        OverviewFactsText.Text = string.Empty;
        OverviewRulesText.Text = string.Empty;
        _busy = true;
        try
        {
            using var api = new TestPlatformApiClient(_settings.ServerUrl);
            var overview = await api.GetOverviewAsync(assignmentId);
            if (!IsVisible)
            {
                return;
            }

            _overview = overview;
            OverviewTitleText.Text = overview.Title;
            OverviewDescriptionText.Text = overview.Description;
            OverviewFactsText.Text = TestRunnerText.OverviewFacts(overview.QuestionCount, overview.MaximumScore, overview.AuthorName, overview.TimeLimitSeconds);
            OverviewRulesText.Text = TestRunnerText.OverviewRules + Environment.NewLine
                + (overview.ResultPolicy.ShowScore ? TestRunnerText.ScoreVisibleRule : TestRunnerText.ScoreHiddenRule);
            OverviewContinueButton.IsEnabled = true;
            StatusTextBlock.Text = TestRunnerText.StatusReady;
        }
        catch
        {
            OverviewTitleText.Text = TestRunnerText.OverviewFailed;
            OverviewRetryButton.IsVisible = true;
            StatusTextBlock.Text = TestRunnerText.OverviewFailed;
        }
        finally
        {
            _busy = false;
        }
    }

    private async void OverviewRetry_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_overviewAssignmentId is not null)
        {
            await LoadOverviewAsync(_overviewAssignmentId);
        }
    }

    private async void OverviewContinue_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_overview is null || _busy)
        {
            return;
        }

        if (_student is null)
        {
            ShowPanel(identity: true);
            SurnameTextBox.Focus();
        }
        else
        {
            await StartSelectedAssignmentAsync(_overview.AssignmentPublicId);
        }
    }

    private async Task StartSelectedAssignmentAsync(string assignmentId)
    {
        if (_api is null || _student is null || _busy)
        {
            return;
        }

        await RunBusyAsync(TestRunnerText.LoadingTest, async () =>
        {
            var response = await _api.StartAttemptAsync(new StartAttemptRequest(assignmentId, _student));
            await LoadImagesAsync(response);
            BeginAttempt(response);
        });
    }

    private void LocalizeStudentFlow()
    {
        TestMenu.Header = TestRunnerText.TestMenuLabel;
        ViewMenu.Header = TestRunnerText.ViewMenuLabel;
        TestInfoMenuItem.Header = TestRunnerText.OverviewHeading;
        FinishMenuItem.Header = TestRunnerText.FinishCommand;
        ZoomInMenuItem.Header = TestRunnerText.ZoomIn;
        ZoomOutMenuItem.Header = TestRunnerText.ZoomOut;
        ResetZoomMenuItem.Header = TestRunnerText.ResetZoom;
        OverviewHeadingText.Text = TestRunnerText.OverviewHeading;
        OverviewRetryButton.Content = TestRunnerText.RetryCommand;
        OverviewContinueButton.Content = TestRunnerText.ReadAndContinue;
        SkipQuestionButton.Content = TestRunnerText.SkipCommand;
        ApplyContentScale();
    }

    private void ZoomIn_OnClick(object? sender, RoutedEventArgs e) => SetContentScale(_contentScale + 0.15);

    private void ZoomOut_OnClick(object? sender, RoutedEventArgs e) => SetContentScale(_contentScale - 0.15);

    private void ResetZoom_OnClick(object? sender, RoutedEventArgs e) => SetContentScale(1);

    private void SetContentScale(double scale)
    {
        CaptureCurrentAnswer();
        _contentScale = Math.Clamp(scale, 0.85, 1.75);
        ApplyContentScale();
    }

    private void ApplyContentScale()
    {
        // Inherited type size scales answer controls; the image editor keeps original pixel coordinates.
        FontSize = 16;
        QuestionPromptText.FontSize = 18 * _contentScale;
        AnswerHintText.FontSize = 16 * _contentScale;
        AnswerHost.SetValue(TextBlock.FontSizeProperty, 16 * _contentScale);
        OverviewDescriptionText.FontSize = 16 * _contentScale;
        OverviewFactsText.FontSize = 16 * _contentScale;
        OverviewRulesText.FontSize = 16 * _contentScale;
        if (_currentEditor is ImagePointEditor image)
        {
            image.SetScale(_contentScale);
        }
    }

    private async void TestInfoMenuItem_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_busy || _sessionDialogOpen)
        {
            return;
        }

        var text = _attempt is { } attempt
            ? attempt.Assignment.Title + Environment.NewLine + attempt.TestDefinition.Description
                + Environment.NewLine + TestRunnerText.OverviewFacts(_questions.Count, _questions.Sum(q => q.Score),
                    attempt.TestDefinition.Author?.Name, attempt.Assignment.AttemptPolicy.TimeLimitSeconds ?? attempt.TestDefinition.Settings.TimeLimitSeconds)
            : OverviewTitleText.Text + Environment.NewLine + OverviewDescriptionText.Text + Environment.NewLine + OverviewFactsText.Text;
        var close = new Button { Content = TestRunnerText.DoneCommand };
        var dialog = new Window
        {
            Title = TestRunnerText.OverviewHeading,
            Width = 520,
            Height = 400,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = new DockPanel
            {
                Margin = new Thickness(20),
                Children =
                {
                    new Border { [DockPanel.DockProperty] = Dock.Bottom, Child = close, Padding = new Thickness(0, 12, 0, 0) },
                    new ScrollViewer { Content = new TextBlock { Text = text, TextWrapping = Avalonia.Media.TextWrapping.Wrap } },
                },
            },
        };
        close.Click += (_, _) => dialog.Close();
        await ShowSessionDialogAsync<object?>(dialog);
    }

    private void StartProgressClock()
    {
        _timeExpired = false;
        _retrySubmitAfterUtc = DateTime.MinValue;
        _progressTimer.Stop();
        _progressTimer.Tick -= UpdateAttemptProgress;
        _progressTimer.Tick += UpdateAttemptProgress;
        _progressTimer.Start();
        UpdateProgressDisplay();
    }

    private DateTime? AttemptDeadline()
    {
        if (_attempt is null)
        {
            return null;
        }

        var limit = _attempt.Assignment.AttemptPolicy.TimeLimitSeconds ?? _attempt.TestDefinition.Settings.TimeLimitSeconds;
        var deadline = limit is > 0 ? _attempt.StartedAtUtc.AddSeconds(limit.Value) : (DateTime?)null;
        var end = _attempt.Assignment.Availability.EndUtc;
        return end is not null && (deadline is null || end < deadline) ? end : deadline;
    }

    private async void UpdateAttemptProgress(object? sender, EventArgs e)
    {
        if (!AttemptPanel.IsVisible || _attempt is null)
        {
            return;
        }

        UpdateProgressDisplay();
        if (AttemptDeadline() is { } deadline && DateTime.UtcNow >= deadline)
        {
            _timeExpired = true;
            AnswerHost.IsEnabled = false;
            PreviousQuestionButton.IsEnabled = false;
            NextQuestionButton.IsEnabled = false;
            SkipQuestionButton.IsEnabled = false;
            if (!_busy && !_sessionDialogOpen && DateTime.UtcNow >= _retrySubmitAfterUtc)
            {
                _retrySubmitAfterUtc = DateTime.UtcNow.AddSeconds(10);
                await SubmitCurrentAsync();
            }
        }
    }

    private void UpdateProgressDisplay()
    {
        if (_attempt is null)
        {
            return;
        }

        CaptureCurrentAnswer();
        AnsweredProgressText.Text = TestRunnerText.Answered(_answers.Count, _questions.Count);
        AttemptProgressBar.Value = _questions.Count == 0 ? 0 : 100d * _answers.Count / _questions.Count;
        ElapsedText.Text = TestRunnerText.Elapsed(DateTime.UtcNow - _attempt.StartedAtUtc);
        RemainingText.Text = AttemptDeadline() is { } deadline ? TestRunnerText.Remaining(deadline - DateTime.UtcNow) : TestRunnerText.NoTimeLimit;
    }

    private void SkipQuestionButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_busy || _timeExpired || _questions.Count == 0)
        {
            return;
        }

        CaptureCurrentAnswer();
        _questionIndex = _questionIndex < _questions.Count - 1 ? _questionIndex + 1
            : Math.Max(0, _questions.FindIndex(question => !_answers.ContainsKey(question.Id)));
        RefreshQuestionChrome();
        RebuildCurrentEditor();
    }

    private async Task SubmitCurrentAsync()
    {
        if (_busy || _api is null || _attempt is null)
        {
            return;
        }

        _busy = true;
        AnswerHost.IsEnabled = false;
        StatusTextBlock.Text = _timeExpired ? TestRunnerText.TimeExpired : TestRunnerText.Connecting;
        try
        {
            CaptureCurrentAnswer();
            var response = await _api.SubmitAsync(_attempt.AttemptPublicId, _attempt.AttemptToken,
                new SubmitAttemptRequest(BuildAnswers(isFinal: true), ReplaceAnswers: true));
            if (IsVisible)
            {
                ShowResult(response);
            }
        }
        catch
        {
            StatusTextBlock.Text = TestRunnerText.SubmitFailed;
            AnswerHost.IsEnabled = !_timeExpired;
        }
        finally
        {
            _busy = false;
        }
    }

    private void PopulateResultSummary(SubmitAttemptResponse response)
    {
        _progressTimer.Stop();
        FinishMenuItem.IsEnabled = false;
        ResultTitleText.Text = _attempt?.Assignment.Title;
        ResultStudentText.Text = _student is { } student ? $"{student.Surname} {student.Name}" : string.Empty;
        ResultGradeText.IsVisible = response.ResultView.ShowScore && response.Result.Grade is not null;
        ResultGradeText.Text = response.Result.Grade is { } grade ? TestRunnerText.GradeLine(grade) : string.Empty;
        var unanswered = Math.Max(0, _questions.Count - _answers.Count);
        ResultSummaryText.Text = TestRunnerText.Answered(_answers.Count, _questions.Count) + Environment.NewLine + TestRunnerText.Unanswered(unanswered);
        if (response.ResultView.ShowPerQuestionFeedback)
        {
            var answered = response.Result.QuestionResults.Where(q => _answers.ContainsKey(q.QuestionId)).ToList();
            ResultSummaryText.Text += Environment.NewLine + TestRunnerText.ResultCounts(
                answered.Count(q => q.IsCorrect), answered.Count(q => !q.IsCorrect && q.ScoreEarned > 0),
                answered.Count(q => !q.IsCorrect && q.ScoreEarned == 0));
        }

        ResultTimeText.Text = _attempt is null ? string.Empty : TestRunnerText.Elapsed(response.SubmittedAtUtc - _attempt.StartedAtUtc);
        ResultDeliveryText.Text = TestRunnerText.ResultConfirmed + Environment.NewLine + response.SubmittedAtUtc.ToLocalTime().ToString("g");
        (_currentEditor as IDisposable)?.Dispose();
        _currentEditor = null;
        AnswerHost.Child = null;
    }
}
