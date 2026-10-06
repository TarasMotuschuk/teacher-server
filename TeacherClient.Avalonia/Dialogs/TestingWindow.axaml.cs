using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
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
    private readonly DispatcherTimer _monitorTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    private bool _monitorRefreshing;
    private bool _updatingAssignments;
    private bool _closed;
    private bool _busy;
    private bool _connected;

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
        TestsGrid.ItemsSource = _tests;
        AssignmentsGrid.ItemsSource = _assignments;
        AttemptsGrid.ItemsSource = _attempts;
        ResultsGrid.ItemsSource = _results;
        AttemptsGrid.SelectionChanged += (_, _) =>
        {
            if (AttemptsGrid.SelectedItem is not null)
            {
                ResultsGrid.SelectedItem = null;
            }
        };
        ResultsGrid.SelectionChanged += (_, _) =>
        {
            if (ResultsGrid.SelectedItem is not null)
            {
                AttemptsGrid.SelectedItem = null;
            }
        };
        ApplyLocalization();
        ConfigureColumns();
        MonitorAssignmentComboBox.ItemsSource = _assignments;
        MonitorAssignmentComboBox.SelectionChanged += MonitorAssignmentChanged;
        LaunchAssignmentComboBox.SelectionChanged += (_, _) =>
        {
            if (!_updatingAssignments && ResolveLaunchAssignment() is { } assignment)
            {
                MonitorAssignmentComboBox.SelectedItem = assignment;
            }
        };
        MainTabControl.SelectionChanged += async (_, e) =>
        {
            if (ReferenceEquals(e.Source, MainTabControl) && MainTabControl.SelectedItem == MonitorTabItem && !_busy)
            {
                await RefreshMonitorAsync();
            }
        };
        _monitorTimer.Tick += async (_, _) =>
        {
            if (!_busy && MainTabControl.SelectedItem == MonitorTabItem)
            {
                await RefreshMonitorAsync();
            }
        };
        Opened += async (_, _) =>
        {
            await ConnectToTeacherPlatformAsync();
            if (!_closed)
            {
                _monitorTimer.Start();
            }
        };
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
        LaunchHintText.Text = CrossPlatformText.TestingLaunchHint;
        LaunchMenuItem.Header = CrossPlatformText.TestingLaunchMenu;
        ToolsMenuItem.Header = CrossPlatformText.TestingToolsMenu;
        DeployRunnerMenuItem.Header = CrossPlatformText.TestingDeployRunner;
        StartSelectedMenuItem.Header = CrossPlatformText.TestingChooseStudents;
        LaunchAssignmentLabel.Text = CrossPlatformText.TestingLaunchAssignment;
        ExitCodesMenuItem.Header = CrossPlatformText.TestingExitCodes;
        StartAllOnlineMenuItem.Header = CrossPlatformText.TestingStartAllOnline;
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
        MonitorStatusText.Text = CrossPlatformText.TestingMonitorChooseAssignment;
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
        AssignmentsGrid.Columns.Add(CreateTextColumn(CrossPlatformText.TestingColumnTitle, nameof(AssignmentRow.Title), 3));
        AssignmentsGrid.Columns.Add(CreateTextColumn(CrossPlatformText.TestingColumnStatus, nameof(AssignmentRow.Status), 1));
        AssignmentsGrid.Columns.Add(CreateTextColumn(CrossPlatformText.TestingColumnTest, nameof(AssignmentRow.TestRef), 1.5));

        AttemptsGrid.Columns.Clear();
        AttemptsGrid.Columns.Add(CreateTextColumn(CrossPlatformText.TestingColumnStudent, nameof(AttemptRow.Student), 2));
        AttemptsGrid.Columns.Add(CreateTextColumn(CrossPlatformText.TestingColumnStatus, nameof(AttemptRow.Status), 1));
        AttemptsGrid.Columns.Add(CreateTextColumn(CrossPlatformText.TestingColumnProgress, nameof(AttemptRow.Progress), 1));
        AttemptsGrid.Columns.Add(CreateTextColumn(CrossPlatformText.TestingColumnStarted, nameof(AttemptRow.StartedAt), 1.5));

        ResultsGrid.Columns.Clear();
        ResultsGrid.Columns.Add(CreateTextColumn(CrossPlatformText.TestingColumnStudent, nameof(ResultRow.Student), 2));
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

    private async Task ConnectToTeacherPlatformAsync()
    {
        if (_busy)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            _connected = false;
            var url = TestPlatformHost.DefaultLocalUrl;
            _api?.Dispose();
            _api = new TestPlatformApiClient(url);
            try
            {
                await _api.CheckHealthAsync();
            }
            catch
            {
                if (!TestPlatformHost.TryStart(out _))
                {
                    throw new InvalidOperationException(CrossPlatformText.TestingPlatformNotFound);
                }

                var connected = false;
                for (var i = 0; i < 20; i++)
                {
                    await Task.Delay(250);
                    try
                    {
                        await _api.CheckHealthAsync();
                        connected = true;
                        break;
                    }
                    catch
                    {
                    }
                }

                if (!connected)
                {
                    throw new InvalidOperationException(CrossPlatformText.TestingPlatformStartFailed);
                }
            }

            _connected = true;
            _settings = _settings with { TestPlatformBaseUrl = url };
            _settingsStore.Save(_settings);
            ConnectionStatusText.Text = CrossPlatformText.TestingConnectedOnTeacherPc;
            StatusTextBlock.Text = CrossPlatformText.TestingConnectedOnTeacherPc;
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
            var created = await _api!.CreateAssignmentAsync(new CreateAssignmentRequest(
                test.PublicId,
                test.Version,
                draft.Title,
                new AssignmentAudienceDto(AudienceType.Class, ClassPublicId: null, StudentPublicIds: null),
                new AssignmentAvailabilityDto(null, null),
                new AttemptPolicyDto(draft.MaxAttempts, draft.TimeLimitSeconds),
                new ResultPolicyDto(draft.ShowScore, draft.ShowCorrectAnswers, draft.ShowPerQuestionFeedback)));

            StatusTextBlock.Text = CrossPlatformText.TestingAssignmentCreated;
            await RefreshAssignmentsAsync();
            LaunchAssignmentComboBox.SelectedItem = _assignments.FirstOrDefault(item => item.PublicId == created.PublicId);
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

        MonitorAssignmentComboBox.SelectedItem = assignment;
        MainTabControl.SelectedItem = MonitorTabItem;
        await RunBusyAsync(RefreshMonitorAsync);
    }

    private async void DeployRunnerMenuItem_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
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

    private async void StartSelectedMenuItem_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => await ChooseRecipientsAndStartAsync(selectAll: false);

    private async void StartAllOnlineMenuItem_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => await ChooseRecipientsAndStartAsync(selectAll: true);

    private async Task ChooseRecipientsAndStartAsync(bool selectAll)
    {
        if (_busy || !await EnsureConnectedAsync())
        {
            return;
        }

        var assignment = ResolveLaunchAssignment();
        if (assignment is null)
        {
            await ConfirmationDialog.ShowInfoAsync(this, CrossPlatformText.Validation, CrossPlatformText.TestingSelectAssignmentFirst);
            LaunchAssignmentComboBox.Focus();
            return;
        }

        var online = _getOnlineAgents();
        if (online.Count == 0)
        {
            await ConfirmationDialog.ShowInfoAsync(this, CrossPlatformText.Validation, CrossPlatformText.TestingNoOnlineAgents);
            return;
        }

        var agents = await TestRecipientsDialog.ShowAsync(this, assignment.Title, online,
            selectAll ? online : _getSelectedAgents());
        if (agents is { Count: > 0 })
        {
            await RunBusyAsync(async () => await StartRunnerAsync(agents));
        }
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

    private async void ExitCodesMenuItem_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await RunBusyAsync(async () =>
        {
            var codes = TestExitCodeStore.Load().Reverse().Select(entry =>
                $"{entry.CreatedAt:g} — {entry.Assignment} ({entry.Machines}){Environment.NewLine}{CrossPlatformText.TestingExitPin(entry.Code)}");
            var text = new TextBox
            {
                Text = string.Join(Environment.NewLine + Environment.NewLine, codes),
                IsReadOnly = true,
                AcceptsReturn = true,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Margin = new Avalonia.Thickness(16),
            };
            var dialog = new Window
            {
                Title = CrossPlatformText.TestingExitCodes,
                Width = 680,
                Height = 420,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = text,
            };
            await dialog.ShowDialog(this);
        });
    }

    private async Task StartRunnerAsync(IReadOnlyList<DiscoveredAgentRow> agents)
    {
        var assignment = ResolveLaunchAssignment();
        if (assignment is null)
        {
            await ConfirmationDialog.ShowInfoAsync(this, CrossPlatformText.Validation, CrossPlatformText.TestingSelectAssignmentFirst);
            return;
        }

        var exitCode = RandomNumberGenerator.GetInt32(100000, 1000000).ToString(CultureInfo.InvariantCulture);
        var exitCodeHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(exitCode)));
        TestExitCodeStore.Add(assignment.Title, exitCode, string.Join(", ", agents.Select(agent => agent.MachineName)));
        var classroomUrl = TestClassroomLaunchHelper.ResolveClassroomServerUrl(TestPlatformHost.DefaultLocalUrl);
        StatusTextBlock.Text = CrossPlatformText.TestingClassroomUrl(classroomUrl);

        var failures = new List<string>();
        var succeeded = 0;
        foreach (var agent in agents)
        {
            try
            {
                var client = new TeacherApiClient($"http://{agent.RespondingAddress}:{agent.Port}", _settings.SharedSecret);
                var script = TestClassroomLaunchHelper.BuildLaunchScript(classroomUrl, assignment.PublicId, agent, exitCodeHash);
                await client.ExecuteRemoteCommandAsync(script, RemoteCommandRunAs.CurrentUser);
                succeeded++;
            }
            catch (Exception ex)
            {
                failures.Add($"{agent.MachineName}: {ex.Message}");
            }
        }

        if (succeeded > 0)
        {
            MonitorAssignmentComboBox.SelectedItem = assignment;
            MainTabControl.SelectedItem = MonitorTabItem;
            await RefreshMonitorAsync();
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
        var selectedId = ResolveLaunchAssignment()?.PublicId;
        var monitorId = _monitorAssignmentId;
        _updatingAssignments = true;
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
        var open = _assignments.Where(item => string.Equals(item.Status, "Published", StringComparison.OrdinalIgnoreCase)).ToList();
        LaunchAssignmentComboBox.ItemsSource = open;
        LaunchAssignmentComboBox.SelectedItem = open.FirstOrDefault(item => item.PublicId == selectedId)
            ?? (open.Count == 1 ? open[0] : null);
        _updatingAssignments = false;
        MonitorAssignmentComboBox.SelectedItem = _assignments.FirstOrDefault(item => item.PublicId == monitorId)
            ?? ResolveLaunchAssignment();
        SelectMonitorAssignment();
    }

    private async void MonitorAssignmentChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_updatingAssignments)
        {
            return;
        }

        SelectMonitorAssignment();
        if (!_busy && MainTabControl.SelectedItem == MonitorTabItem)
        {
            await RefreshMonitorAsync();
        }
    }

    private void SelectMonitorAssignment()
    {
        _monitorAssignmentId = (MonitorAssignmentComboBox.SelectedItem as AssignmentRow)?.PublicId;
        _attempts.Clear();
        _results.Clear();
        MonitorStatusText.Text = _monitorAssignmentId is null
            ? CrossPlatformText.TestingMonitorChooseAssignment
            : CrossPlatformText.TestingMonitorWaiting;
    }

    private async Task RefreshMonitorAsync()
    {
        if (_closed || _monitorRefreshing || _api is null || string.IsNullOrWhiteSpace(_monitorAssignmentId))
        {
            return;
        }

        var assignmentId = _monitorAssignmentId;
        _monitorRefreshing = true;
        try
        {
            var attempts = await _api.ListAttemptsAsync(assignmentId);
            var results = await _api.ListResultsAsync(assignmentId);
            if (_closed || assignmentId != _monitorAssignmentId)
            {
                return;
            }

            var selectedAttemptId = (AttemptsGrid.SelectedItem as AttemptRow)?.AttemptPublicId;
            var selectedResultId = (ResultsGrid.SelectedItem as ResultRow)?.AttemptPublicId;
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

            var students = _attempts.ToDictionary(item => item.AttemptPublicId, item => item.Student);
            _results.Clear();
            foreach (var item in results.Items.OrderByDescending(x => x.CompletedAtUtc))
            {
                _results.Add(new ResultRow(
                    item.AttemptPublicId,
                    students.GetValueOrDefault(item.AttemptPublicId, "—"),
                    $"{item.ScoreEarned:0.##}/{item.ScoreMax:0.##}",
                    $"{item.Percent:0.##}",
                    item.CompletedAtUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)));
            }

            AttemptsGrid.SelectedItem = _attempts.FirstOrDefault(item => item.AttemptPublicId == selectedAttemptId);
            ResultsGrid.SelectedItem = _results.FirstOrDefault(item => item.AttemptPublicId == selectedResultId);
            MonitorStatusText.Text = CrossPlatformText.TestingMonitorUpdated(_attempts.Count, _results.Count);
        }
        catch (Exception ex)
        {
            if (!_closed && assignmentId == _monitorAssignmentId)
            {
                MonitorStatusText.Text = $"{CrossPlatformText.TestingMonitorFailed} {ex.Message}";
            }
        }
        finally
        {
            _monitorRefreshing = false;
        }
    }

    private AssignmentRow? ResolveLaunchAssignment()
        => LaunchAssignmentComboBox.SelectedItem as AssignmentRow;

    private async Task<bool> EnsureConnectedAsync()
    {
        if (_connected)
        {
            return true;
        }

        await ConnectToTeacherPlatformAsync();
        return _connected;
    }

    private async Task RunBusyAsync(Func<Task> action)
    {
        if (_busy)
        {
            return;
        }

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
        _closed = true;
        _monitorTimer.Stop();
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
    string Student,
    string Score,
    string Percent,
    string CompletedAt);
