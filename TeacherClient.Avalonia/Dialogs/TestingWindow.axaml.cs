using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Teacher.Common.Contracts;
using Teacher.Common.Contracts.Testing;
using TeacherClient.CrossPlatform.Localization;
using TeacherClient.CrossPlatform.Models;
using TeacherClient.CrossPlatform.Services;

namespace TeacherClient.CrossPlatform.Dialogs;

public partial class TestingWindow : Window
{
    private readonly ClientSettingsStore _settingsStore;
    private readonly Func<IReadOnlyList<DiscoveredAgentRow>> _getSelectedAgents;
    private readonly Func<IReadOnlyList<DiscoveredAgentRow>> _getOnlineAgents;
    private readonly ObservableCollection<TestDefinitionRow> _tests = [];
    private readonly ObservableCollection<AssignmentRow> _assignments = [];
    private readonly ObservableCollection<AttemptRow> _attempts = [];
    private readonly ObservableCollection<ResultRow> _results = [];

    private ClientSettings _settings;
    private TestPlatformApiClient? _api;
    private string? _monitorAssignmentId;
    private string? _monitorAssignmentTitle;
    private bool _busy;

    public TestingWindow()
        : this(ClientSettings.Default, new ClientSettingsStore(), () => [], () => [])
    {
    }

    public TestingWindow(
        ClientSettings settings,
        ClientSettingsStore settingsStore,
        Func<IReadOnlyList<DiscoveredAgentRow>> getSelectedAgents,
        Func<IReadOnlyList<DiscoveredAgentRow>> getOnlineAgents)
    {
        InitializeComponent();
        Icon = AppIconLoader.Load();
        _settings = settings;
        _settingsStore = settingsStore;
        _getSelectedAgents = getSelectedAgents;
        _getOnlineAgents = getOnlineAgents;
        ServerUrlTextBox.Text = settings.TestPlatformBaseUrl;
        TestsGrid.ItemsSource = _tests;
        AssignmentsGrid.ItemsSource = _assignments;
        AttemptsGrid.ItemsSource = _attempts;
        ResultsGrid.ItemsSource = _results;
        ApplyLocalization();
        ConfigureColumns();
    }

    public static async Task ShowAsync(
        Window owner,
        ClientSettings settings,
        ClientSettingsStore settingsStore,
        Func<IReadOnlyList<DiscoveredAgentRow>> getSelectedAgents,
        Func<IReadOnlyList<DiscoveredAgentRow>> getOnlineAgents)
    {
        var dialog = new TestingWindow(settings, settingsStore, getSelectedAgents, getOnlineAgents);
        await dialog.ShowDialog(owner);
    }

    private void ApplyLocalization()
    {
        Title = CrossPlatformText.TestingWindowTitle;
        ServerUrlLabel.Text = CrossPlatformText.TestPlatformBaseUrl;
        ConnectButton.Content = CrossPlatformText.TestingConnect;
        LaunchClassLabel.Text = CrossPlatformText.TestingLaunchClassLabel;
        LaunchHintText.Text = CrossPlatformText.TestingLaunchHint;
        DeployRunnerButton.Content = CrossPlatformText.TestingDeployRunner;
        StartSelectedButton.Content = CrossPlatformText.TestingStartSelected;
        StartAllOnlineButton.Content = CrossPlatformText.TestingStartAllOnline;
        TestsTabItem.Header = CrossPlatformText.TestingTabTests;
        AssignmentsTabItem.Header = CrossPlatformText.TestingTabAssignments;
        MonitorTabItem.Header = CrossPlatformText.TestingTabMonitor;
        RefreshTestsButton.Content = CrossPlatformText.TestingRefresh;
        ImportMyTestButton.Content = CrossPlatformText.TestingImportMyTest;
        ImportCctestButton.Content = CrossPlatformText.TestingImportCctest;
        CreateAssignmentButton.Content = CrossPlatformText.TestingCreateAssignment;
        RefreshAssignmentsButton.Content = CrossPlatformText.TestingRefresh;
        CloseAssignmentButton.Content = CrossPlatformText.TestingCloseAssignment;
        OpenMonitorButton.Content = CrossPlatformText.TestingOpenMonitor;
        RefreshMonitorButton.Content = CrossPlatformText.TestingRefresh;
        ViewResultButton.Content = CrossPlatformText.TestingViewDetails;
        AttemptsHeadingText.Text = CrossPlatformText.TestingAttemptsHeading;
        ResultsHeadingText.Text = CrossPlatformText.TestingResultsHeading;
        MonitorAssignmentLabel.Text = CrossPlatformText.TestingMonitorAssignment;
        MonitorAssignmentTitleText.Text = _monitorAssignmentTitle ?? "—";
        ConfigureColumns();
    }

    private void ConfigureColumns()
    {
        TestsGrid.Columns.Clear();
        TestsGrid.Columns.Add(CreateTextColumn(CrossPlatformText.TestingColumnTitle, nameof(TestDefinitionRow.Title), 3));
        TestsGrid.Columns.Add(CreateTextColumn(CrossPlatformText.TestingColumnVersion, nameof(TestDefinitionRow.Version), 0.8));
        TestsGrid.Columns.Add(CreateTextColumn(CrossPlatformText.TestingColumnQuestions, nameof(TestDefinitionRow.QuestionCount), 1));
        TestsGrid.Columns.Add(CreateTextColumn(CrossPlatformText.TestingColumnUpdated, nameof(TestDefinitionRow.UpdatedAt), 1.5));

        AssignmentsGrid.Columns.Clear();
        AssignmentsGrid.Columns.Add(CreateTextColumn(CrossPlatformText.TestingColumnTitle, nameof(AssignmentRow.Title), 2.5));
        AssignmentsGrid.Columns.Add(CreateTextColumn(CrossPlatformText.TestingColumnStatus, nameof(AssignmentRow.Status), 1));
        AssignmentsGrid.Columns.Add(CreateTextColumn(CrossPlatformText.TestingColumnTest, nameof(AssignmentRow.TestRef), 1.5));
        AssignmentsGrid.Columns.Add(CreateTextColumn(CrossPlatformText.TestingColumnStudent, nameof(AssignmentRow.Audience), 1.2));

        AttemptsGrid.Columns.Clear();
        AttemptsGrid.Columns.Add(CreateTextColumn(CrossPlatformText.TestingColumnStudent, nameof(AttemptRow.Student), 2));
        AttemptsGrid.Columns.Add(CreateTextColumn(CrossPlatformText.TestingColumnStatus, nameof(AttemptRow.Status), 1));
        AttemptsGrid.Columns.Add(CreateTextColumn(CrossPlatformText.TestingColumnProgress, nameof(AttemptRow.Progress), 1));
        AttemptsGrid.Columns.Add(CreateTextColumn(CrossPlatformText.TestingColumnStarted, nameof(AttemptRow.StartedAt), 1.5));

        ResultsGrid.Columns.Clear();
        ResultsGrid.Columns.Add(CreateTextColumn(CrossPlatformText.TestingColumnScore, nameof(ResultRow.Score), 1.2));
        ResultsGrid.Columns.Add(CreateTextColumn(CrossPlatformText.TestingColumnPercent, nameof(ResultRow.Percent), 0.8));
        ResultsGrid.Columns.Add(CreateTextColumn(CrossPlatformText.TestingColumnCompleted, nameof(ResultRow.CompletedAt), 1.5));
        ResultsGrid.Columns.Add(CreateTextColumn("ID", nameof(ResultRow.AttemptPublicId), 2));
    }

    private static DataGridTextColumn CreateTextColumn(string header, string propertyName, double starWidth)
        => new()
        {
            Header = header,
            Binding = new Avalonia.Data.Binding(propertyName),
            Width = new DataGridLength(starWidth, DataGridLengthUnitType.Star),
        };

    private async void ConnectButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_busy)
        {
            return;
        }

        var url = ServerUrlTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(url))
        {
            await ConfirmationDialog.ShowInfoAsync(this, CrossPlatformText.Validation, CrossPlatformText.TestPlatformBaseUrl);
            return;
        }

        await RunBusyAsync(async () =>
        {
            _api?.Dispose();
            _api = new TestPlatformApiClient(url);
            await _api.CheckHealthAsync();
            _settings = _settings with { TestPlatformBaseUrl = url.TrimEnd('/') };
            _settingsStore.Save(_settings);
            ConnectionStatusText.Text = CrossPlatformText.TestingConnected;
            StatusTextBlock.Text = CrossPlatformText.TestingConnected;
            await RefreshTestsAsync();
            await RefreshAssignmentsAsync();
        });
    }

    private async void RefreshTestsButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => await RunBusyAsync(RefreshTestsAsync);

    private async void RefreshAssignmentsButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => await RunBusyAsync(RefreshAssignmentsAsync);

    private async void RefreshMonitorButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => await RunBusyAsync(RefreshMonitorAsync);

    private async void ImportMyTestButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_busy || !await EnsureConnectedAsync())
        {
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = CrossPlatformText.TestingImportMyTest,
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("MyTest XML")
                {
                    Patterns = ["*.xml"],
                },
            ],
        });

        var file = files.Count > 0 ? files[0] : null;
        if (file is null)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            var imported = await _api!.ImportMyTestXmlAsync(file.Path.LocalPath);
            StatusTextBlock.Text = $"{CrossPlatformText.TestingImportSuccess} {imported.TestDefinition.Title}";
            await RefreshTestsAsync();
        });
    }

    private async void ImportCctestButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_busy || !await EnsureConnectedAsync())
        {
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = CrossPlatformText.TestingImportCctest,
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("cctest")
                {
                    Patterns = ["*.cctest"],
                },
            ],
        });

        var file = files.Count > 0 ? files[0] : null;
        if (file is null)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            var imported = await _api!.ImportCctestAsync(file.Path.LocalPath);
            StatusTextBlock.Text = $"{CrossPlatformText.TestingImportSuccess} {imported.TestDefinition.Title}";
            await RefreshTestsAsync();
        });
    }

    private async void CreateAssignmentButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_busy || !await EnsureConnectedAsync())
        {
            return;
        }

        if (TestsGrid.SelectedItem is not TestDefinitionRow test)
        {
            await ConfirmationDialog.ShowInfoAsync(this, CrossPlatformText.Validation, CrossPlatformText.TestingSelectTestFirst);
            return;
        }

        var draft = await CreateAssignmentDialog.ShowAsync(this, test.Title);
        if (draft is null)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            await _api!.CreateAssignmentAsync(new CreateAssignmentRequest(
                test.PublicId,
                test.Version,
                draft.Title,
                new AssignmentAudienceDto(AudienceType.Class, draft.ClassName, null),
                new AssignmentAvailabilityDto(null, null),
                new AttemptPolicyDto(draft.MaxAttempts, draft.TimeLimitSeconds),
                new ResultPolicyDto(draft.ShowScore, draft.ShowCorrectAnswers, draft.ShowPerQuestionFeedback)));

            StatusTextBlock.Text = CrossPlatformText.TestingAssignmentCreated;
            LaunchClassTextBox.Text = draft.ClassName;
            await RefreshAssignmentsAsync();
            MainTabControl.SelectedItem = AssignmentsTabItem;
        });
    }

    private async void CloseAssignmentButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_busy || !await EnsureConnectedAsync())
        {
            return;
        }

        if (AssignmentsGrid.SelectedItem is not AssignmentRow assignment)
        {
            await ConfirmationDialog.ShowInfoAsync(this, CrossPlatformText.Validation, CrossPlatformText.TestingSelectAssignmentFirst);
            return;
        }

        if (!await ConfirmationDialog.ShowAsync(this, CrossPlatformText.TestingCloseAssignment, CrossPlatformText.TestingConfirmCloseAssignment))
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            await _api!.CloseAssignmentAsync(assignment.PublicId);
            StatusTextBlock.Text = CrossPlatformText.TestingAssignmentClosed;
            await RefreshAssignmentsAsync();
            if (string.Equals(_monitorAssignmentId, assignment.PublicId, StringComparison.Ordinal))
            {
                await RefreshMonitorAsync();
            }
        });
    }

    private async void OpenMonitorButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_busy || !await EnsureConnectedAsync())
        {
            return;
        }

        if (AssignmentsGrid.SelectedItem is not AssignmentRow assignment)
        {
            await ConfirmationDialog.ShowInfoAsync(this, CrossPlatformText.Validation, CrossPlatformText.TestingSelectAssignmentFirst);
            return;
        }

        _monitorAssignmentId = assignment.PublicId;
        _monitorAssignmentTitle = assignment.Title;
        MonitorAssignmentTitleText.Text = assignment.Title;
        MainTabControl.SelectedItem = MonitorTabItem;
        await RunBusyAsync(RefreshMonitorAsync);
    }

    private async void DeployRunnerButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_busy)
        {
            return;
        }

        var agents = _getSelectedAgents().Count > 0
            ? _getSelectedAgents()
            : _getOnlineAgents();
        if (agents.Count == 0)
        {
            await ConfirmationDialog.ShowInfoAsync(this, CrossPlatformText.Validation, CrossPlatformText.TestingChooseAgentsFirst);
            return;
        }

        await RunBusyAsync(async () => await DeployRunnerAsync(agents));
    }

    private async void StartSelectedButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_busy)
        {
            return;
        }

        var agents = _getSelectedAgents();
        if (agents.Count == 0)
        {
            await ConfirmationDialog.ShowInfoAsync(this, CrossPlatformText.Validation, CrossPlatformText.TestingChooseAgentsFirst);
            return;
        }

        await RunBusyAsync(async () => await StartRunnerAsync(agents));
    }

    private async void StartAllOnlineButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_busy)
        {
            return;
        }

        var agents = _getOnlineAgents();
        if (agents.Count == 0)
        {
            await ConfirmationDialog.ShowInfoAsync(this, CrossPlatformText.Validation, CrossPlatformText.TestingNoOnlineAgents);
            return;
        }

        await RunBusyAsync(async () => await StartRunnerAsync(agents));
    }

    private async Task DeployRunnerAsync(IReadOnlyList<DiscoveredAgentRow> agents)
    {
        List<string>? files = null;
        var failures = new List<string>();
        var succeeded = 0;
        var skippedInstalled = 0;
        foreach (var agent in agents)
        {
            try
            {
                StatusTextBlock.Text = $"{agent.MachineName}…";
                var client = new TeacherApiClient($"http://{agent.RespondingAddress}:{agent.Port}", _settings.SharedSecret);
                if (await TestClassroomLaunchHelper.HasInstalledRunnerAsync(client))
                {
                    // The ClassCommander installer already ships TestRunner on this PC and the
                    // agent auto-update keeps it current; no upload needed.
                    skippedInstalled++;
                    continue;
                }

                if (files is null)
                {
                    var localDir = TestClassroomLaunchHelper.FindLocalRunnerDirectory();
                    files = localDir is null
                        ? []
                        : Directory.GetFiles(localDir, "*", SearchOption.TopDirectoryOnly)
                            .Where(path => !path.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase))
                            .ToList();
                    if (files.Count == 0)
                    {
                        await ConfirmationDialog.ShowInfoAsync(this, CrossPlatformText.Error, CrossPlatformText.TestingRunnerNotBuilt);
                        return;
                    }
                }

                await client.EnsureSharedWritableDirectoryAsync(TestClassroomLaunchHelper.DefaultRemoteDirectory);
                foreach (var file in files)
                {
                    await client.UploadFileAsync(file, TestClassroomLaunchHelper.DefaultRemoteDirectory);
                }

                succeeded++;
            }
            catch (Exception ex)
            {
                failures.Add($"{agent.MachineName}: {ex.Message}");
            }
        }

        var status = failures.Count == 0
            ? CrossPlatformText.TestingDeployCompleted(succeeded)
            : CrossPlatformText.TestingDeployCompletedWithFailures(succeeded, failures.Count);
        if (skippedInstalled > 0)
        {
            status = $"{status} {CrossPlatformText.TestingDeploySkippedInstalled(skippedInstalled)}";
        }

        StatusTextBlock.Text = status;
        if (failures.Count > 0)
        {
            await ConfirmationDialog.ShowInfoAsync(this, CrossPlatformText.Error, string.Join(Environment.NewLine, failures));
        }
    }

    private async Task StartRunnerAsync(IReadOnlyList<DiscoveredAgentRow> agents)
    {
        var className = LaunchClassTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(className))
        {
            await ConfirmationDialog.ShowInfoAsync(this, CrossPlatformText.Validation, CrossPlatformText.TestingLaunchClassRequired);
            return;
        }

        var configuredUrl = ServerUrlTextBox.Text?.Trim() ?? _settings.TestPlatformBaseUrl;
        var classroomUrl = TestClassroomLaunchHelper.ResolveClassroomServerUrl(configuredUrl);
        StatusTextBlock.Text = CrossPlatformText.TestingClassroomUrl(classroomUrl);

        var failures = new List<string>();
        var succeeded = 0;
        foreach (var agent in agents)
        {
            try
            {
                var client = new TeacherApiClient($"http://{agent.RespondingAddress}:{agent.Port}", _settings.SharedSecret);
                var script = TestClassroomLaunchHelper.BuildLaunchScript(classroomUrl, className, agent);
                await client.ExecuteRemoteCommandAsync(script, RemoteCommandRunAs.CurrentUser);
                succeeded++;
            }
            catch (Exception ex)
            {
                failures.Add($"{agent.MachineName}: {ex.Message}");
            }
        }

        StatusTextBlock.Text = failures.Count == 0
            ? CrossPlatformText.TestingStartCompleted(succeeded)
            : CrossPlatformText.TestingStartCompletedWithFailures(succeeded, failures.Count);
        if (failures.Count > 0)
        {
            await ConfirmationDialog.ShowInfoAsync(this, CrossPlatformText.Error, string.Join(Environment.NewLine, failures));
        }
    }

    private async void ViewResultButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_busy || !await EnsureConnectedAsync())
        {
            return;
        }

        var attemptId = AttemptsGrid.SelectedItem is AttemptRow attempt
            ? attempt.AttemptPublicId
            : ResultsGrid.SelectedItem is ResultRow result
                ? result.AttemptPublicId
                : null;

        if (string.IsNullOrWhiteSpace(attemptId))
        {
            await ConfirmationDialog.ShowInfoAsync(this, CrossPlatformText.Validation, CrossPlatformText.TestingSelectAttemptFirst);
            return;
        }

        await RunBusyAsync(async () =>
        {
            var attempt = await _api!.GetAttemptAsync(attemptId);
            ResultDto? result = null;
            try
            {
                result = await _api.GetAttemptResultAsync(attemptId);
            }
            catch
            {
                // Attempt may still be in progress.
            }

            await AttemptDetailDialog.ShowAsync(this, attempt, result);
        });
    }

    private async Task RefreshTestsAsync()
    {
        if (_api is null)
        {
            return;
        }

        var page = await _api.ListTestDefinitionsAsync();
        _tests.Clear();
        foreach (var item in page.Items.OrderByDescending(x => x.UpdatedAtUtc))
        {
            _tests.Add(new TestDefinitionRow(
                item.PublicId,
                item.Version,
                item.Title,
                item.QuestionCount,
                item.UpdatedAtUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)));
        }
    }

    private async Task RefreshAssignmentsAsync()
    {
        if (_api is null)
        {
            return;
        }

        var page = await _api.ListAssignmentsAsync();
        _assignments.Clear();
        foreach (var item in page.Items.OrderByDescending(x => x.Title, StringComparer.CurrentCultureIgnoreCase))
        {
            _assignments.Add(new AssignmentRow(
                item.PublicId,
                item.Title,
                item.Status.ToString(),
                $"{item.TestPublicId} v{item.TestVersion}",
                item.Audience.ClassPublicId ?? item.Audience.Type.ToString()));
        }
    }

    private async Task RefreshMonitorAsync()
    {
        if (_api is null || string.IsNullOrWhiteSpace(_monitorAssignmentId))
        {
            return;
        }

        var attempts = await _api.ListAttemptsAsync(_monitorAssignmentId);
        _attempts.Clear();
        foreach (var item in attempts.Items.OrderByDescending(x => x.StartedAtUtc))
        {
            var student = $"{item.Student.Surname} {item.Student.Name}".Trim();
            if (!string.IsNullOrWhiteSpace(item.Student.ClassName))
            {
                student += $" ({item.Student.ClassName})";
            }

            _attempts.Add(new AttemptRow(
                item.AttemptPublicId,
                student,
                item.Status.ToString(),
                $"{item.AnsweredCount}/{item.QuestionCount}",
                item.StartedAtUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)));
        }

        var results = await _api.ListResultsAsync(_monitorAssignmentId);
        _results.Clear();
        foreach (var item in results.Items.OrderByDescending(x => x.CompletedAtUtc))
        {
            _results.Add(new ResultRow(
                item.AttemptPublicId,
                $"{item.ScoreEarned:0.##}/{item.ScoreMax:0.##}",
                $"{item.Percent:0.##}",
                item.CompletedAtUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)));
        }
    }

    private async Task<bool> EnsureConnectedAsync()
    {
        if (_api is not null)
        {
            return true;
        }

        await ConfirmationDialog.ShowInfoAsync(this, CrossPlatformText.Validation, CrossPlatformText.TestingConnect);
        StatusTextBlock.Text = CrossPlatformText.TestingConnect;
        return false;
    }

    private async Task RunBusyAsync(Func<Task> action)
    {
        _busy = true;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            await ConfirmationDialog.ShowInfoAsync(this, CrossPlatformText.Error, ex.Message);
            StatusTextBlock.Text = ex.Message;
        }
        finally
        {
            _busy = false;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _api?.Dispose();
        base.OnClosed(e);
    }
}

internal sealed record TestDefinitionRow(
    string PublicId,
    int Version,
    string Title,
    int QuestionCount,
    string UpdatedAt);

internal sealed record AssignmentRow(
    string PublicId,
    string Title,
    string Status,
    string TestRef,
    string Audience);

internal sealed record AttemptRow(
    string AttemptPublicId,
    string Student,
    string Status,
    string Progress,
    string StartedAt);

internal sealed record ResultRow(
    string AttemptPublicId,
    string Score,
    string Percent,
    string CompletedAt);
