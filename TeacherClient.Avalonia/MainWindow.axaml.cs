using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Teacher.Common;
using Teacher.Common.Localization;
using Teacher.Common.Contracts;
using Teacher.Common.Vnc;
using TeacherClient.CrossPlatform.Dialogs;
using TeacherClient.CrossPlatform.Localization;
using TeacherClient.CrossPlatform.Models;
using TeacherClient.CrossPlatform.Services;

namespace TeacherClient.CrossPlatform;

public partial class MainWindow : Window
{
    private readonly AgentDiscoveryService _agentDiscoveryService = new();
    private readonly ManualAgentStore _manualAgentStore = new();
    private readonly ClientSettingsStore _clientSettingsStore = new();
    private readonly FrequentProgramStore _frequentProgramStore = new();
    private readonly TeacherUpdatePreparationService _updatePreparationService =
        new(GetUpdatePreparationRootDirectory());
    private readonly TeacherClientUpdateService _clientUpdateService =
        new(GetClientUpdateRootDirectory(), typeof(MainWindow).Assembly.GetName().Version?.ToString() ?? "0.0.0");
    private readonly ObservableCollection<DiscoveredAgentRow> _agents = [];
    private readonly ObservableCollection<ProcessInfoDto> _processes = [];
    private readonly ObservableCollection<FileSystemEntryDto> _localEntries = [];
    private readonly ObservableCollection<FileSystemEntryDto> _remoteEntries = [];
    private readonly ObservableCollection<RegistryNode> _registryRoots = [];
    private readonly ObservableCollection<RegistryValueDto> _registryValues = [];
    private readonly ObservableCollection<RemoteManagementTileViewModel> _remoteManagementTiles = [];
    private readonly DispatcherTimer _agentRefreshTimer = new();
    private readonly DispatcherTimer _connectionMonitorTimer = new();
    private readonly DispatcherTimer _updateStatusTimer = new();
    private readonly HashSet<string> _preparedStudentWorkFolders = new(StringComparer.OrdinalIgnoreCase);
    private ClientSettings _clientSettings = ClientSettings.Default;
    private List<ManualAgentEntry> _manualAgents = [];
    private List<FrequentProgramEntry> _frequentPrograms = [];
    private List<DiscoveredAgentRow> _allAgents = [];
    private bool _suppressDriveSelection;
    private bool _isClosing;
    private string? _remoteParentPath;
    private string? _lastConnectedAgentId;
    private string? _lastConnectedServerUrl;
    private string? _lastConnectedMachineName;
    private string? _remoteManagementSelectedAgentId;

    public MainWindow()
    {
        _clientSettings = _clientSettingsStore.Load();
        CrossPlatformText.SetLanguage(_clientSettings.Language);
        InitializeComponent();
        ProcessesGrid.ItemsSource = _processes;
        LocalFilesGrid.ItemsSource = _localEntries;
        RemoteFilesGrid.ItemsSource = _remoteEntries;
        AgentsGrid.ItemsSource = _agents;
        RegistryTreeView.ItemsSource = _registryRoots;
        RegistryValuesGrid.ItemsSource = _registryValues;
        RemoteManagementListBox.ItemsSource = _remoteManagementTiles;
        RegistryTreeView.AddHandler(TreeViewItem.ExpandedEvent, RegistryTreeView_NodeExpanded);
        RemoteManagementListBox.SelectionChanged += RemoteManagementListBox_OnSelectionChanged;
        InitializeRegistryTree();
        LocalPathTextBox.Text = GetDefaultLocalPath();
        _manualAgents = _manualAgentStore.Load().ToList();
        _frequentPrograms = _frequentProgramStore.Load().ToList();
        PopulateLocalRoots();
        _ = LoadLocalDirectoryAsync(LocalPathTextBox.Text);

        GroupFilterComboBox.ItemsSource = new[] { CrossPlatformText.AllGroups };
        GroupFilterComboBox.SelectedIndex = 0;
        StatusFilterComboBox.ItemsSource = new[] { CrossPlatformText.All, CrossPlatformText.Online, CrossPlatformText.Offline, CrossPlatformText.Unknown };
        StatusFilterComboBox.SelectedIndex = 0;
        ApplyLocalization();

        _agentRefreshTimer.Interval = TimeSpan.FromSeconds(15);
        _agentRefreshTimer.Tick += async (_, _) => await LoadAgentsAsync();
        _connectionMonitorTimer.Interval = TimeSpan.FromSeconds(10);
        _connectionMonitorTimer.Tick += async (_, _) => await MonitorConnectionAsync();
        _updateStatusTimer.Interval = TimeSpan.FromSeconds(5);
        _updateStatusTimer.Tick += async (_, _) => await PollAgentUpdateStatusesAsync();

        Opened += async (_, _) =>
        {
            await LoadAgentsAsync();
            _agentRefreshTimer.Start();
            _connectionMonitorTimer.Start();
            _updateStatusTimer.Start();
        };
        Closing += (_, _) =>
        {
            _isClosing = true;
            _agentRefreshTimer.Stop();
            _connectionMonitorTimer.Stop();
            _updateStatusTimer.Stop();
            DisposeRemoteManagementTiles();
            _updatePreparationService.Dispose();
            _clientUpdateService.Dispose();
        };
    }

    private static string GetUpdatePreparationRootDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var baseDirectory = string.IsNullOrWhiteSpace(localAppData)
            ? Path.Combine(AppContext.BaseDirectory, "data", "updates")
            : Path.Combine(localAppData, "TeacherServer", "TeacherClient.Avalonia", "updates");
        Directory.CreateDirectory(baseDirectory);
        return baseDirectory;
    }

    private static string GetClientUpdateRootDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var baseDirectory = string.IsNullOrWhiteSpace(localAppData)
            ? Path.Combine(AppContext.BaseDirectory, "data", "client-updates")
            : Path.Combine(localAppData, "TeacherServer", "TeacherClient.Avalonia", "client-updates");
        Directory.CreateDirectory(baseDirectory);
        return baseDirectory;
    }

    private TeacherApiClient CreateClient() => new(GetCurrentServerUrlOrThrow(), _clientSettings.SharedSecret);

    private async void BrowserLockCheckBox_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not CheckBox checkBox || checkBox.Tag is not DiscoveredAgentRow agent)
        {
            return;
        }

        await ToggleBrowserLockAsync(agent, checkBox.IsChecked == true);
    }

    private async void InputLockCheckBox_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not CheckBox checkBox || checkBox.Tag is not DiscoveredAgentRow agent)
        {
            return;
        }

        await ToggleInputLockAsync(agent, checkBox.IsChecked == true);
    }

    private async void SettingsButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var dialog = new SettingsWindow(_clientSettings);
        var result = await dialog.ShowDialog<bool>(this);
        if (!result)
        {
            return;
        }

        _clientSettings = dialog.ToSettings();
        _clientSettingsStore.Save(_clientSettings);
        CrossPlatformText.SetLanguage(_clientSettings.Language);
        ApplyLocalization();
        SetStatus(CrossPlatformText.SettingsSaved);

        if (_allAgents.Count > 0)
        {
            await LoadAgentsAsync();
        }

        _preparedStudentWorkFolders.Clear();
        await EnsureStudentWorkFolderOnAvailableAgentsAsync(reportSummary: true);
        await ApplyStudentPolicySettingsToOnlineAgentsAsync(reportSummary: true);
    }

    private async void RefreshAgentsButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await LoadAgentsAsync();
    }

    private async void ConnectSelectedAgentButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await ConnectSelectedAgentAsync();
    }

    private async void SaveDesktopIconLayoutMenuItem_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await SaveDesktopIconLayoutAsync();
    }

    private async void RestoreDesktopIconLayoutMenuItem_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await RestoreDesktopIconLayoutAsync();
    }

    private async void RestoreDesktopIconsSelectedMenuItem_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var targetAgents = GetSelectedAgents();
        if (targetAgents.Count == 0)
        {
            SetStatus(CrossPlatformText.ChooseAgentsForDistribution);
            return;
        }

        await RestoreDesktopIconsOnAgentsAsync(FilterOutCurrentConnectedAgent(targetAgents));
    }

    private async void RestoreDesktopIconsAllMenuItem_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var targetAgents = _allAgents
            .Where(x => string.Equals(x.Status, CrossPlatformText.Online, StringComparison.OrdinalIgnoreCase))
            .ToList();

        await RestoreDesktopIconsOnAgentsAsync(FilterOutCurrentConnectedAgent(targetAgents));
    }

    private async void ApplyCurrentDesktopIconsSelectedMenuItem_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var targetAgents = GetSelectedAgents();
        if (targetAgents.Count == 0)
        {
            SetStatus(CrossPlatformText.ChooseAgentsForDistribution);
            return;
        }

        await ApplyCurrentDesktopLayoutToAgentsAsync(FilterOutCurrentConnectedAgent(targetAgents));
    }

    private async void ApplyCurrentDesktopIconsAllMenuItem_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var targetAgents = _allAgents
            .Where(x => string.Equals(x.Status, CrossPlatformText.Online, StringComparison.OrdinalIgnoreCase))
            .ToList();

        await ApplyCurrentDesktopLayoutToAgentsAsync(FilterOutCurrentConnectedAgent(targetAgents));
    }

    private async void CheckSelectedAgentUpdateMenuItem_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await CheckSelectedAgentUpdateAsync();
    }

    private async void StartSelectedAgentUpdateMenuItem_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await StartSelectedAgentUpdateAsync();
    }

    private async void AddManualAgentButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var dialog = new ManualAgentWindow();
        var result = await dialog.ShowDialog<bool>(this);
        if (!result)
        {
            return;
        }

        var entry = dialog.ToEntry();
        _manualAgents.Add(entry);
        SaveManualAgents();
        await LoadAgentsAsync();
        SetStatus(CrossPlatformText.AddedManualAgent(entry.DisplayName));
    }

    private async void EditManualAgentButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (AgentsGrid.SelectedItem is not DiscoveredAgentRow agent || !agent.IsManual)
        {
            SetStatus(CrossPlatformText.ChooseManualAgentFirst);
            return;
        }

        var existing = _manualAgents.FirstOrDefault(x => string.Equals(x.Id, agent.AgentId, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            SetStatus(CrossPlatformText.ManualAgentNotFound);
            return;
        }

        var dialog = new ManualAgentWindow(existing);
        var result = await dialog.ShowDialog<bool>(this);
        if (!result)
        {
            return;
        }

        var updated = dialog.ToEntry(existing.Id);
        var index = _manualAgents.FindIndex(x => string.Equals(x.Id, existing.Id, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            _manualAgents[index] = updated;
            SaveManualAgents();
            await LoadAgentsAsync();
            SetStatus(CrossPlatformText.UpdatedManualAgent(updated.DisplayName));
        }
    }

    private async void RemoveManualAgentButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (AgentsGrid.SelectedItem is not DiscoveredAgentRow agent || !agent.IsManual)
        {
            SetStatus(CrossPlatformText.ChooseManualAgentFirst);
            return;
        }

        if (!await ConfirmationDialog.ShowAsync(this, CrossPlatformText.RemoveManualAgentTitle, CrossPlatformText.RemoveManualAgentPrompt(agent.MachineName)))
        {
            return;
        }

        _manualAgents = _manualAgents
            .Where(x => !string.Equals(x.Id, agent.AgentId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        SaveManualAgents();
        await LoadAgentsAsync();
        SetStatus(CrossPlatformText.RemovedManualAgent(agent.MachineName));
    }

    private async Task LoadAgentsAsync()
    {
        await RunBusyAsync(async () =>
        {
            var discoveredAgents = await _agentDiscoveryService.DiscoverAsync();
            var discoveredRows = discoveredAgents.Select(DiscoveredAgentRow.FromDto).ToList();
            var manualRows = _manualAgents.Select(DiscoveredAgentRow.FromManualEntry).ToList();
            var merged = MergeAgents(manualRows, discoveredRows).ToList();
            _allAgents = (await UpdateAgentStatusesAsync(merged, discoveredRows)).ToList();
            RefreshGroupFilterOptions();
            ApplyAgentFilters();
            await RefreshRemoteManagementTilesAsync();
            await EnsureStudentWorkFolderOnAvailableAgentsAsync(reportSummary: false);

            SetStatus(_allAgents.Count == 0
                ? CrossPlatformText.NoAgentsAvailable
                : BuildAgentAvailabilityStatus(discoveredAgents.Count, _manualAgents.Count));
        }, CrossPlatformText.DiscoveryError);
    }

    private async Task RefreshFrequentProgramsAsync()
    {
        var targetAgents = _allAgents
            .Where(x => string.Equals(x.Status, CrossPlatformText.Online, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (targetAgents.Count == 0)
        {
            SetStatus(CrossPlatformText.NoOnlineAgentsAvailableForGroupCommand);
            return;
        }

        var collected = new List<FrequentProgramEntry>(_frequentPrograms);
        var failures = new List<string>();

        await RunBusyAsync(async () =>
        {
            for (var index = 0; index < targetAgents.Count; index++)
            {
                var agent = targetAgents[index];
                SetStatus(CrossPlatformText.RemoteCommandProgress(agent.MachineName, index + 1, targetAgents.Count));

                try
                {
                    var client = new TeacherApiClient($"http://{agent.RespondingAddress}:{agent.Port}", _clientSettings.SharedSecret);
                    var shortcuts = await client.GetPublicDesktopShortcutsAsync();
                    collected.AddRange(shortcuts
                        .Where(x => !string.IsNullOrWhiteSpace(x.DisplayName) && !string.IsNullOrWhiteSpace(x.CommandText))
                        .Select(x => FrequentProgramEntry.Create(x.DisplayName, x.CommandText, RemoteCommandRunAs.CurrentUser)));
                }
                catch (Exception ex)
                {
                    failures.Add($"{agent.MachineName}: {ex.Message}");
                }
            }

            _frequentProgramStore.Save(collected);
            _frequentPrograms = _frequentProgramStore.Load().ToList();
            SetStatus(CrossPlatformText.FrequentProgramsRefreshed(_frequentPrograms.Count));

            if (failures.Count > 0)
            {
                await ConfirmationDialog.ShowInfoAsync(
                    this,
                    CrossPlatformText.FrequentProgramsRefreshError,
                    string.Join(Environment.NewLine, failures));
            }
        }, CrossPlatformText.FrequentProgramsRefreshError);
    }

    private async Task ExecuteRemoteCommandOnAgentsAsync(IReadOnlyList<DiscoveredAgentRow> targetAgents, bool selectedOnly)
    {
        var submission = await RemoteCommandWindow.ShowAsync(this, _frequentPrograms);
        if (submission is null)
        {
            return;
        }

        if (!await ConfirmationDialog.ShowAsync(
                this,
                CrossPlatformText.GroupCommandsTitle,
                CrossPlatformText.RemoteCommandPrompt(targetAgents.Count, selectedOnly)))
        {
            return;
        }

        var failures = new List<string>();
        var succeeded = 0;

        await RunBusyAsync(async () =>
        {
            for (var index = 0; index < targetAgents.Count; index++)
            {
                var agent = targetAgents[index];
                try
                {
                    SetStatus(CrossPlatformText.RemoteCommandProgress(agent.MachineName, index + 1, targetAgents.Count));
                    var client = new TeacherApiClient($"http://{agent.RespondingAddress}:{agent.Port}", _clientSettings.SharedSecret);
                    await client.ExecuteRemoteCommandAsync(submission.Script, submission.RunAs);
                    succeeded++;
                }
                catch (Exception ex)
                {
                    failures.Add($"{agent.MachineName}: {ex.Message}");
                }
            }

            SetStatus(
                failures.Count == 0
                    ? CrossPlatformText.RemoteCommandCompleted(succeeded)
                    : CrossPlatformText.RemoteCommandCompletedWithFailures(succeeded, failures.Count));

            if (failures.Count > 0)
            {
                await ConfirmationDialog.ShowInfoAsync(
                    this,
                    CrossPlatformText.BulkCommandsResultTitle,
                    string.Join(Environment.NewLine, failures));
            }
        }, CrossPlatformText.BulkCommandsResultTitle);
    }

    private async Task ConnectSelectedAgentAsync()
    {
        if (AgentsGrid.SelectedItem is not DiscoveredAgentRow agent)
        {
            SetStatus(CrossPlatformText.ChooseAgentFirst);
            return;
        }

        await ConnectToServerAsync($"http://{agent.RespondingAddress}:{agent.Port}", agent, agent.Source);
    }

    private async Task CheckSelectedAgentUpdateAsync()
    {
        var dialog = new UpdatePreparationWindow(_updatePreparationService);
        await dialog.ShowDialog(this);
    }

    private async Task SaveDesktopIconLayoutAsync()
    {
        await RunDesktopIconLayoutActionAsync(
            save: true,
            execute: client => client.SaveDesktopIconLayoutAsync());
    }

    private async Task RestoreDesktopIconLayoutAsync()
    {
        await RunDesktopIconLayoutActionAsync(
            save: false,
            execute: client => client.RestoreDesktopIconLayoutAsync());
    }

    private async Task StartSelectedAgentUpdateAsync()
    {
        if (AgentsGrid.SelectedItem is not DiscoveredAgentRow agent)
        {
            SetStatus(CrossPlatformText.ChooseAgentFirst);
            return;
        }

        if (!string.Equals(agent.Status, CrossPlatformText.Online, StringComparison.OrdinalIgnoreCase))
        {
            SetStatus(CrossPlatformText.AgentUpdateRequiresOnlineAgent);
            return;
        }

        try
        {
            var client = new TeacherApiClient($"http://{agent.RespondingAddress}:{agent.Port}", _clientSettings.SharedSecret);
            var request = await CreatePreparedUpdateRequestAsync(agent, client);
            if (request is null)
            {
                SetStatus(CrossPlatformText.UpdatePreparationMissing);
                return;
            }
            var status = await client.StartAgentUpdateAsync(request);
            if (status is not null)
            {
                ReplaceAgentRow(ApplyUpdateStatus(agent, status));
            }
            SetStatus(CrossPlatformText.AgentUpdateStarted(agent.MachineName, status?.AvailableVersion ?? "?"));
        }
        catch (Exception ex)
        {
            SetStatus($"{CrossPlatformText.AgentUpdateStartFailed}: {ex.Message}");
        }
    }

    private async Task RunDesktopIconLayoutActionAsync(
        bool save,
        Func<TeacherApiClient, Task<DesktopIconLayoutOperationResultDto?>> execute)
    {
        if (string.IsNullOrWhiteSpace(_lastConnectedServerUrl))
        {
            SetStatus(CrossPlatformText.ConnectFromAgentsTabFirst);
            return;
        }

        await RunBusyAsync(async () =>
        {
            var client = CreateClient();
            var result = await execute(client);
            var machineName = _lastConnectedMachineName ?? "PC";
            var iconCount = result?.IconCount ?? 0;
            SetStatus(save
                ? CrossPlatformText.DesktopIconLayoutSaved(machineName, iconCount)
                : CrossPlatformText.DesktopIconLayoutRestored(machineName, iconCount));
        }, CrossPlatformText.DesktopIconLayoutError);
    }

    private async Task RestoreDesktopIconsOnAgentsAsync(IReadOnlyList<DiscoveredAgentRow> targetAgents)
    {
        if (targetAgents.Count == 0)
        {
            SetStatus(CrossPlatformText.NoOnlineAgentsAvailableForGroupCommand);
            return;
        }

        var failures = new List<string>();
        var succeeded = 0;

        await RunBusyAsync(async () =>
        {
            for (var index = 0; index < targetAgents.Count; index++)
            {
                var agent = targetAgents[index];
                try
                {
                    SetStatus(CrossPlatformText.DesktopIconLayoutBulkProgress(agent.MachineName, index + 1, targetAgents.Count));
                    var client = new TeacherApiClient($"http://{agent.RespondingAddress}:{agent.Port}", _clientSettings.SharedSecret);
                    await client.RestoreDesktopIconLayoutAsync();
                    succeeded++;
                }
                catch (Exception ex)
                {
                    failures.Add($"{agent.MachineName}: {ex.Message}");
                }
            }
        }, CrossPlatformText.DesktopIconLayoutError);

        SetStatus(failures.Count == 0
            ? CrossPlatformText.DesktopIconLayoutBulkCompleted(succeeded)
            : CrossPlatformText.DesktopIconLayoutBulkCompletedWithFailures(succeeded, failures.Count));

        if (failures.Count > 0)
        {
            await ConfirmationDialog.ShowInfoAsync(
                this,
                CrossPlatformText.DesktopIconLayoutError,
                string.Join(Environment.NewLine, failures));
        }
    }

    private async Task ApplyCurrentDesktopLayoutToAgentsAsync(IReadOnlyList<DiscoveredAgentRow> targetAgents)
    {
        if (string.IsNullOrWhiteSpace(_lastConnectedServerUrl))
        {
            SetStatus(CrossPlatformText.ConnectFromAgentsTabFirst);
            return;
        }

        if (targetAgents.Count == 0)
        {
            SetStatus(CrossPlatformText.NoOnlineAgentsAvailableForGroupCommand);
            return;
        }

        DesktopIconLayoutSnapshotDto sourceLayout;
        try
        {
            var sourceClient = CreateClient();
            await sourceClient.SaveDesktopIconLayoutAsync();
            sourceLayout = await sourceClient.GetDesktopIconLayoutAsync()
                ?? throw new InvalidOperationException("Desktop icon layout snapshot is unavailable.");
        }
        catch (Exception ex)
        {
            SetStatus($"{CrossPlatformText.DesktopIconLayoutError}: {ex.Message}");
            return;
        }

        var failures = new List<string>();
        var succeeded = 0;

        await RunBusyAsync(async () =>
        {
            for (var index = 0; index < targetAgents.Count; index++)
            {
                var agent = targetAgents[index];
                try
                {
                    SetStatus(CrossPlatformText.DesktopIconLayoutApplyBulkProgress(agent.MachineName, index + 1, targetAgents.Count));
                    var client = new TeacherApiClient($"http://{agent.RespondingAddress}:{agent.Port}", _clientSettings.SharedSecret);
                    await client.ApplyDesktopIconLayoutAsync(sourceLayout, restoreAfterApply: true);
                    succeeded++;
                }
                catch (Exception ex)
                {
                    failures.Add($"{agent.MachineName}: {ex.Message}");
                }
            }
        }, CrossPlatformText.DesktopIconLayoutError);

        SetStatus(failures.Count == 0
            ? CrossPlatformText.DesktopIconLayoutAppliedBulkCompleted(succeeded)
            : CrossPlatformText.DesktopIconLayoutAppliedBulkCompletedWithFailures(succeeded, failures.Count));

        if (failures.Count > 0)
        {
            await ConfirmationDialog.ShowInfoAsync(
                this,
                CrossPlatformText.DesktopIconLayoutError,
                string.Join(Environment.NewLine, failures));
        }
    }

    private async Task ConnectToServerAsync(string serverUrl, DiscoveredAgentRow? agent, string sourceLabel)
    {
        var client = new TeacherApiClient(serverUrl, _clientSettings.SharedSecret);
        var info = await client.GetServerInfoAsync();
        if (info is null)
        {
            SetStatus(CrossPlatformText.ConnectionFailed);
            return;
        }

        _lastConnectedAgentId = agent?.AgentId;
        _lastConnectedServerUrl = serverUrl;
        _lastConnectedMachineName = info.MachineName;
        try
        {
            await ApplyStudentPolicySettingsToAgentAsync(client);
        }
        catch
        {
            // Best-effort policy sync on connect should not block regular teacher connection flow.
        }
        SetStatus(CrossPlatformText.ConnectedToAgent(sourceLabel, info.MachineName, NormalizeUserDisplay(info.CurrentUser, info.MachineName), info.AgentVersion));
        await LoadProcessesAsync();
        await LoadLocalDirectoryAsync(LocalPathTextBox.Text);
        await LoadRemoteDirectoryAsync(RemotePathTextBox.Text);
    }

    private async Task ToggleBrowserLockAsync(DiscoveredAgentRow agent, bool enabled)
    {
        if (!string.Equals(agent.Status, CrossPlatformText.Online, StringComparison.OrdinalIgnoreCase))
        {
            SetStatus(CrossPlatformText.BrowserLockRequiresOnlineAgent);
            ApplyAgentFilters();
            return;
        }

        try
        {
            var client = new TeacherApiClient($"http://{agent.RespondingAddress}:{agent.Port}", _clientSettings.SharedSecret);
            await client.SetBrowserLockEnabledAsync(enabled);
            ReplaceAgentRow(agent with { BrowserLockEnabled = enabled });
            SetStatus(enabled ? CrossPlatformText.BrowserLockEnabledFor(agent.MachineName) : CrossPlatformText.BrowserLockDisabledFor(agent.MachineName));
        }
        catch (Exception ex)
        {
            SetStatus($"{CrossPlatformText.BrowserLockToggleFailed}: {ex.Message}");
            ApplyAgentFilters();
        }
    }

    private async Task ToggleInputLockAsync(DiscoveredAgentRow agent, bool enabled)
    {
        if (!string.Equals(agent.Status, CrossPlatformText.Online, StringComparison.OrdinalIgnoreCase))
        {
            SetStatus(CrossPlatformText.InputLockRequiresOnlineAgent);
            ApplyAgentFilters();
            return;
        }

        try
        {
            var client = new TeacherApiClient($"http://{agent.RespondingAddress}:{agent.Port}", _clientSettings.SharedSecret);
            await client.SetInputLockEnabledAsync(enabled);
            ReplaceAgentRow(agent with { InputLockEnabled = enabled });
            SetStatus(enabled ? CrossPlatformText.InputLockEnabledFor(agent.MachineName) : CrossPlatformText.InputLockDisabledFor(agent.MachineName));
        }
        catch (Exception ex)
        {
            SetStatus($"{CrossPlatformText.InputLockToggleFailed}: {ex.Message}");
            ApplyAgentFilters();
        }
    }

    private async Task ApplyStudentPolicySettingsToAgentAsync(TeacherApiClient client, CancellationToken cancellationToken = default)
    {
        await client.ApplyStudentPolicySettingsAsync(
            _clientSettings.DesktopIconAutoRestoreMinutes,
            _clientSettings.BrowserLockCheckIntervalSeconds,
            cancellationToken);
    }

    private async Task ApplyStudentPolicySettingsToOnlineAgentsAsync(bool reportSummary)
    {
        var targetAgents = _allAgents
            .Where(x => string.Equals(x.Status, CrossPlatformText.Online, StringComparison.OrdinalIgnoreCase))
            .Where(x => !string.IsNullOrWhiteSpace(x.RespondingAddress))
            .Distinct()
            .ToList();

        if (targetAgents.Count == 0)
        {
            return;
        }

        var succeeded = 0;
        var failed = 0;

        foreach (var agent in targetAgents)
        {
            try
            {
                var client = new TeacherApiClient($"http://{agent.RespondingAddress}:{agent.Port}", _clientSettings.SharedSecret);
                await ApplyStudentPolicySettingsToAgentAsync(client);
                succeeded++;
            }
            catch
            {
                failed++;
            }
        }

        if (!reportSummary)
        {
            return;
        }

        SetStatus(failed == 0
            ? CrossPlatformText.StudentPolicySettingsApplied(succeeded)
            : CrossPlatformText.StudentPolicySettingsAppliedWithFailures(succeeded, failed));
    }

    private string GetCurrentServerUrlOrThrow()
    {
        if (string.IsNullOrWhiteSpace(_lastConnectedServerUrl))
        {
            throw new InvalidOperationException(CrossPlatformText.ConnectFromAgentsTabFirst);
        }

        return _lastConnectedServerUrl;
    }

    private void AgentSearchTextBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        ApplyAgentFilters();
    }

    private void AgentFilterComboBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        ApplyAgentFilters();
    }

    private void ApplyAgentFilters()
    {
        var search = AgentSearchTextBox.Text?.Trim() ?? string.Empty;
        var selectedGroup = GroupFilterComboBox.SelectedItem?.ToString() ?? CrossPlatformText.AllGroups;
        var selectedStatus = StatusFilterComboBox.SelectedItem?.ToString() ?? CrossPlatformText.All;

        var filtered = _allAgents
            .Where(agent => selectedGroup == CrossPlatformText.AllGroups ||
                            string.Equals(agent.GroupName, selectedGroup, StringComparison.OrdinalIgnoreCase))
            .Where(agent => selectedStatus == CrossPlatformText.All ||
                            string.Equals(agent.Status, selectedStatus, StringComparison.OrdinalIgnoreCase))
            .Where(agent =>
                string.IsNullOrWhiteSpace(search) ||
                agent.MachineName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                agent.RespondingAddress.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                agent.CurrentUser.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                agent.Notes.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                agent.GroupName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                agent.MacAddressesDisplay.Contains(search, StringComparison.OrdinalIgnoreCase))
            .ToList();

        ReplaceItems(_agents, filtered);
    }

    private void RefreshGroupFilterOptions()
    {
        var groups = _allAgents
            .Select(x => x.GroupName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Cast<object>()
            .Prepend(CrossPlatformText.AllGroups)
            .ToList();

        var currentSelection = GroupFilterComboBox.SelectedItem?.ToString() ?? CrossPlatformText.AllGroups;
        GroupFilterComboBox.ItemsSource = groups;
        GroupFilterComboBox.SelectedItem = groups.Any(x => string.Equals(x?.ToString(), currentSelection, StringComparison.OrdinalIgnoreCase))
            ? currentSelection
            : CrossPlatformText.AllGroups;
    }

    private async Task<IReadOnlyList<DiscoveredAgentRow>> UpdateAgentStatusesAsync(
        IReadOnlyList<DiscoveredAgentRow> mergedAgents,
        IReadOnlyList<DiscoveredAgentRow> discoveredAgents)
    {
        var onlineEndpoints = discoveredAgents
            .Select(x => $"{x.RespondingAddress}:{x.Port}")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var updatedAgents = new List<DiscoveredAgentRow>(mergedAgents.Count);
        foreach (var agent in mergedAgents)
        {
            var reachabilityClient = new TeacherApiClient(
                $"http://{agent.RespondingAddress}:{agent.Port}",
                _clientSettings.SharedSecret);

            if (onlineEndpoints.Contains($"{agent.RespondingAddress}:{agent.Port}"))
            {
                try
                {
                    var info = await reachabilityClient.GetServerInfoAsync();
                    if (info is not null)
                    {
                        var updateStatus = await reachabilityClient.GetUpdateStatusAsync();
                        var vncStatus = await reachabilityClient.GetVncStatusAsync();
                        updatedAgents.Add(agent with
                        {
                            Status = CrossPlatformText.Online,
                            CurrentUser = NormalizeUserDisplay(info.CurrentUser, info.MachineName),
                            BrowserLockEnabled = info.IsBrowserLockEnabled,
                            InputLockEnabled = info.IsInputLockEnabled,
                            UpdateStatusBadge = CrossPlatformText.UpdateStateBadge(updateStatus),
                            UpdateStatusDetail = CrossPlatformText.FormatUpdateStatusDetail(updateStatus),
                            VncEnabled = vncStatus?.Enabled ?? false,
                            VncRunning = vncStatus?.Running ?? false,
                            VncViewOnly = vncStatus?.ViewOnly ?? true,
                            VncPort = vncStatus?.Port ?? 0,
                            VncStatusMessage = vncStatus?.Message ?? string.Empty,
                            Version = updateStatus?.State == AgentUpdateStateKind.Succeeded
                                ? updateStatus.AvailableVersion ?? info.AgentVersion
                                : info.AgentVersion
                        });
                        continue;
                    }
                }
                catch
                {
                }

                updatedAgents.Add(agent with { Status = CrossPlatformText.Online });
                continue;
            }

            var isReachable = await reachabilityClient.IsServerReachableAsync();
            if (isReachable)
            {
                try
                {
                    var info = await reachabilityClient.GetServerInfoAsync();
                    var updateStatus = await reachabilityClient.GetUpdateStatusAsync();
                    var vncStatus = await reachabilityClient.GetVncStatusAsync();
                    updatedAgents.Add(agent with
                    {
                        Status = CrossPlatformText.Online,
                        CurrentUser = info is null ? agent.CurrentUser : NormalizeUserDisplay(info.CurrentUser, info.MachineName),
                        BrowserLockEnabled = info?.IsBrowserLockEnabled ?? agent.BrowserLockEnabled,
                        InputLockEnabled = info?.IsInputLockEnabled ?? agent.InputLockEnabled,
                        UpdateStatusBadge = CrossPlatformText.UpdateStateBadge(updateStatus),
                        UpdateStatusDetail = CrossPlatformText.FormatUpdateStatusDetail(updateStatus),
                        VncEnabled = vncStatus?.Enabled ?? agent.VncEnabled,
                        VncRunning = vncStatus?.Running ?? agent.VncRunning,
                        VncViewOnly = vncStatus?.ViewOnly ?? agent.VncViewOnly,
                        VncPort = vncStatus?.Port ?? agent.VncPort,
                        VncStatusMessage = vncStatus?.Message ?? agent.VncStatusMessage,
                        Version = updateStatus?.State == AgentUpdateStateKind.Succeeded
                            ? updateStatus.AvailableVersion ?? info?.AgentVersion ?? agent.Version
                            : info?.AgentVersion ?? agent.Version
                    });
                    continue;
                }
                catch
                {
                }
            }

            updatedAgents.Add(agent with
            {
                Status = isReachable ? CrossPlatformText.Online : CrossPlatformText.Offline
            });
        }

        return updatedAgents;
    }

    private async Task MonitorConnectionAsync()
    {
        if (AutoReconnectCheckBox.IsChecked != true || string.IsNullOrWhiteSpace(_lastConnectedServerUrl))
        {
            return;
        }

        try
        {
            var currentClient = new TeacherApiClient(_lastConnectedServerUrl, _clientSettings.SharedSecret);
            if (await currentClient.IsServerReachableAsync())
            {
                return;
            }

            var targetAgent = _allAgents.FirstOrDefault(x =>
                (!string.IsNullOrWhiteSpace(_lastConnectedAgentId) &&
                 string.Equals(x.AgentId, _lastConnectedAgentId, StringComparison.OrdinalIgnoreCase)) ||
                string.Equals($"http://{x.RespondingAddress}:{x.Port}", _lastConnectedServerUrl, StringComparison.OrdinalIgnoreCase));

            if (targetAgent is null || string.Equals(targetAgent.Status, CrossPlatformText.Offline, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            await ConnectToServerAsync($"http://{targetAgent.RespondingAddress}:{targetAgent.Port}", targetAgent, CrossPlatformText.AutoReconnect);
        }
        catch
        {
        }
    }

    private async void RefreshProcessesButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => await LoadProcessesAsync();

    private void RefreshRegistryButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => InitializeRegistryTree();

    private void RegistryTreeView_NodeExpanded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (e.Source is TreeViewItem { DataContext: RegistryNode { IsLoaded: false } node })
            _ = LoadRegistrySubKeysAsync(node);
    }

    private async void RegistryTreeView_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (RegistryTreeView.SelectedItem is not RegistryNode node) return;
        try
        {
            var client = CreateClient();
            var values = await client.GetRegistryValuesAsync(node.Path);
            ReplaceItems(_registryValues, values);
            SetStatus(CrossPlatformText.LoadedRegistryValues(values.Count));
        }
        catch (Exception ex)
        {
            SetStatus($"{CrossPlatformText.RegistryLoadError}: {ex.Message}");
        }
    }

    private async Task LoadRegistryValuesAsync(RegistryNode node)
    {
        try
        {
            var client = CreateClient();
            var values = await client.GetRegistryValuesAsync(node.Path);
            ReplaceItems(_registryValues, values);
            SetStatus(CrossPlatformText.LoadedRegistryValues(values.Count));
        }
        catch (Exception ex)
        {
            SetStatus($"{CrossPlatformText.RegistryLoadError}: {ex.Message}");
        }
    }

    private async Task LoadRegistrySubKeysAsync(RegistryNode node)
    {
        node.IsLoaded = true;
        try
        {
            var client = CreateClient();
            var subKeys = await client.GetRegistrySubKeysAsync(node.Path);
            node.Children.Clear();
            foreach (var key in subKeys)
                node.Children.Add(new RegistryNode(key.Name, key.Path, key.HasChildren));
        }
        catch (Exception ex)
        {
            node.IsLoaded = false;
            SetStatus($"{CrossPlatformText.RegistryLoadError}: {ex.Message}");
        }
    }

    private void InitializeRegistryTree()
    {
        _registryRoots.Clear();
        _registryValues.Clear();
        string[] hives = ["HKEY_LOCAL_MACHINE", "HKEY_CURRENT_USER", "HKEY_CLASSES_ROOT", "HKEY_USERS", "HKEY_CURRENT_CONFIG"];
        foreach (var hive in hives)
            _registryRoots.Add(new RegistryNode(hive, hive, hasChildren: true));
    }

    private async Task HandleNewValueAsync()
    {
        if (RegistryTreeView.SelectedItem is not RegistryNode node)
        {
            return;
        }

        var dialog = new Dialogs.RegistryEditDialog();
        var result = await dialog.ShowDialog<bool?>(this);
        if (result != true) return;

        try
        {
            var client = CreateClient();
            await client.SetRegistryValueAsync(node.Path, dialog.ValueName, dialog.ValueType, dialog.ValueData);
            SetStatus(CrossPlatformText.ValueCreated);
            if (RegistryTreeView.SelectedItem is RegistryNode selectedNode)
                await LoadRegistrySubKeysAsync(selectedNode);
        }
        catch (Exception ex)
        {
            SetStatus($"{CrossPlatformText.RegistryError}: {ex.Message}");
        }
    }

    private void NewValueButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => _ = HandleNewValueAsync();

    private async Task HandleNewKeyAsync()
    {
        if (RegistryTreeView.SelectedItem is not RegistryNode node)
        {
            return;
        }

        var result = await Dialogs.TextInputDialog.ShowAsync(this, CrossPlatformText.NewKey, CrossPlatformText.KeyName);
        if (string.IsNullOrEmpty(result)) return;

        try
        {
            var client = CreateClient();
            await client.CreateRegistryKeyAsync(node.Path, result);
            SetStatus(CrossPlatformText.KeyCreated);
            await LoadRegistrySubKeysAsync(node);
        }
        catch (Exception ex)
        {
            SetStatus($"{CrossPlatformText.RegistryError}: {ex.Message}");
        }
    }

    private void NewKeyButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => _ = HandleNewKeyAsync();

    private async Task HandleEditValueAsync()
    {
        if (RegistryValuesGrid.SelectedItem is not RegistryValueDto value ||
            RegistryTreeView.SelectedItem is not RegistryNode node)
        {
            return;
        }

        try
        {
            var client = CreateClient();
            var editableValue = (await client.GetRegistryValuesForEditAsync(node.Path))
                .FirstOrDefault(x => string.Equals(x.Name, value.Name, StringComparison.Ordinal));
            if (editableValue is null)
            {
                SetStatus($"{CrossPlatformText.RegistryError}: {CrossPlatformText.SelectValueFirst}");
                return;
            }

            var dialog = new Dialogs.RegistryEditDialog(editableValue.Name, editableValue.RawType, editableValue.RawData);
            var result = await dialog.ShowDialog<bool?>(this);
            if (result != true) return;

            await client.SetRegistryValueAsync(node.Path, dialog.ValueName, dialog.ValueType, dialog.ValueData);
            SetStatus(CrossPlatformText.ValueUpdated);
            await LoadRegistryValuesAsync(node);
        }
        catch (Exception ex)
        {
            SetStatus($"{CrossPlatformText.RegistryError}: {ex.Message}");
        }
    }

    private void EditValueButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => _ = HandleEditValueAsync();

    private async Task HandleDeleteValueAsync()
    {
        if (RegistryValuesGrid.SelectedItem is not RegistryValueDto value ||
            RegistryTreeView.SelectedItem is not RegistryNode node)
        {
            return;
        }

        if (!await ConfirmationDialog.ShowAsync(this, CrossPlatformText.Confirmation, CrossPlatformText.ConfirmDeleteValue))
        {
            return;
        }

        try
        {
            var client = CreateClient();
            await client.DeleteRegistryValueAsync(node.Path, value.Name);
            SetStatus(CrossPlatformText.ValueDeleted);
            await LoadRegistryValuesAsync(node);
        }
        catch (Exception ex)
        {
            SetStatus($"{CrossPlatformText.RegistryError}: {ex.Message}");
        }
    }

    private void DeleteValueButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => _ = HandleDeleteValueAsync();

    private async Task HandleDeleteKeyAsync()
    {
        if (RegistryTreeView.SelectedItem is not RegistryNode node)
        {
            return;
        }

        if (!await ConfirmationDialog.ShowAsync(this, CrossPlatformText.Confirmation, CrossPlatformText.ConfirmDeleteKey))
        {
            return;
        }

        try
        {
            var client = CreateClient();
            await client.DeleteRegistryKeyAsync(node.Path);
            SetStatus(CrossPlatformText.KeyDeleted);
            InitializeRegistryTree();
        }
        catch (Exception ex)
        {
            SetStatus($"{CrossPlatformText.RegistryError}: {ex.Message}");
        }
    }

    private void DeleteKeyButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => _ = HandleDeleteKeyAsync();

    private async Task HandleExportRegistryAsync()
    {
        if (RegistryTreeView.SelectedItem is not RegistryNode node)
        {
            SetStatus(CrossPlatformText.SelectKeyFirst);
            return;
        }

        if (StorageProvider is null)
        {
            SetStatus(CrossPlatformText.RegistryExportError);
            return;
        }

        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = CrossPlatformText.ExportRegFile,
            SuggestedFileName = $"{node.Path.Replace('\\', '_')}.reg",
            FileTypeChoices =
            [
                new FilePickerFileType("Registry files")
                {
                    Patterns = ["*.reg"]
                }
            ],
            DefaultExtension = "reg"
        });

        if (file is null)
        {
            return;
        }

        try
        {
            var client = CreateClient();
            var destinationPath = file.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(destinationPath))
            {
                throw new InvalidOperationException("Only local file targets are supported.");
            }

            await client.ExportRegistryKeyAsync(node.Path, destinationPath);
            SetStatus(CrossPlatformText.ExportedRegistryKey(node.Path));
        }
        catch (Exception ex)
        {
            SetStatus($"{CrossPlatformText.RegistryExportError}: {ex.Message}");
        }
    }

    private void ExportRegistryButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => _ = HandleExportRegistryAsync();

    private async Task HandleImportRegistryAsync()
    {
        if (StorageProvider is null)
        {
            SetStatus(CrossPlatformText.RegistryImportError);
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = CrossPlatformText.ImportRegFile,
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Registry files")
                {
                    Patterns = ["*.reg"]
                }
            ]
        });

        var file = files.FirstOrDefault();
        if (file is null)
        {
            return;
        }

        try
        {
            var filePath = file.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new InvalidOperationException("Only local file sources are supported.");
            }

            var client = CreateClient();
            var result = await client.ImportRegistryFileAsync(filePath);

            if (RegistryTreeView.SelectedItem is RegistryNode selectedNode)
            {
                await LoadRegistrySubKeysAsync(selectedNode);
                await LoadRegistryValuesAsync(selectedNode);
            }
            else
            {
                InitializeRegistryTree();
            }

            SetStatus(CrossPlatformText.ImportedRegistryFile(result?.KeysProcessed ?? 0, result?.ValuesProcessed ?? 0));
        }
        catch (Exception ex)
        {
            SetStatus($"{CrossPlatformText.RegistryImportError}: {ex.Message}");
        }
    }

    private void ImportRegistryButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => _ = HandleImportRegistryAsync();

    private async void ProcessesGrid_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (ProcessesGrid.SelectedItem is not ProcessInfoDto process)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            var client = CreateClient();
            var details = await client.GetProcessDetailsAsync(process.Id);
            if (details is null)
            {
                SetStatus(CrossPlatformText.ProcessDetailsLoadError);
                return;
            }

            var action = await ProcessDetailsWindow.ShowAsync(this, details);
            if (action == ProcessActionRequested.Kill)
            {
                if (!await ConfirmationDialog.ShowAsync(this, CrossPlatformText.TerminateProcessTitle, CrossPlatformText.TerminateProcessPrompt(process.Name, process.Id)))
                {
                    return;
                }

                await client.KillProcessAsync(process.Id);
                await LoadProcessesAsync();
                SetStatus(CrossPlatformText.ProcessTerminated(process.Name));
            }
            else if (action == ProcessActionRequested.Restart)
            {
                if (!await ConfirmationDialog.ShowAsync(this, CrossPlatformText.RestartCommand, CrossPlatformText.RestartProcessPrompt(process.Name, process.Id)))
                {
                    return;
                }

                await client.RestartProcessAsync(process.Id);
                await LoadProcessesAsync();
                SetStatus(CrossPlatformText.ProcessRestarted(process.Name));
            }
        }, CrossPlatformText.ProcessDetailsLoadError);
    }

    private async void KillProcessButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (ProcessesGrid.SelectedItem is not ProcessInfoDto process)
        {
            SetStatus(CrossPlatformText.ChooseProcessFirst);
            return;
        }

        if (!await ConfirmationDialog.ShowAsync(this, CrossPlatformText.TerminateProcessTitle, CrossPlatformText.TerminateProcessPrompt(process.Name, process.Id)))
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            var client = CreateClient();
            await client.KillProcessAsync(process.Id);
            await LoadProcessesAsync();
            SetStatus(CrossPlatformText.ProcessTerminated(process.Name));
        });
    }

    private async Task LoadProcessesAsync()
    {
        await RunBusyAsync(async () =>
        {
            var client = CreateClient();
            var processes = await client.GetProcessesAsync();
            ReplaceItems(_processes, processes);
            SetStatus(CrossPlatformText.LoadedProcesses(processes.Count));
        }, CrossPlatformText.ProcessLoadError);
    }

    private async void RefreshFilesButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await LoadLocalDirectoryAsync(LocalPathTextBox.Text);
        await LoadRemoteDirectoryAsync(RemotePathTextBox.Text);
        SetStatus(CrossPlatformText.PanelsRefreshed);
    }

    private async Task LoadLocalDirectoryAsync(string? path)
    {
        await RunBusyAsync(() =>
        {
            var resolvedPath = string.IsNullOrWhiteSpace(path) ? GetDefaultLocalPath() : path!;
            var info = new DirectoryInfo(resolvedPath);
            var entries = info.EnumerateFileSystemInfos()
                .OrderByDescending(x => (x.Attributes & FileAttributes.Directory) == FileAttributes.Directory)
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Select(MapLocalEntry)
                .ToList();

            LocalPathTextBox.Text = info.FullName;
            SelectRoot(LocalDriveComboBox, info.FullName);
            UpdateLocalDriveSpace(info.FullName);
            ReplaceItems(_localEntries, entries);
            return Task.CompletedTask;
        }, CrossPlatformText.LocalBrowseError);
    }

    private async Task LoadRemoteDirectoryAsync(string? path)
    {
        await RunBusyAsync(async () =>
        {
            var client = CreateClient();
            await PopulateRemoteRootsAsync(client);
            var listing = await client.GetRemoteDirectoryAsync(path);
            if (listing is null)
            {
                SetStatus(CrossPlatformText.RemoteListingFailed);
                return;
            }

            RemotePathTextBox.Text = listing.CurrentPath;
            SelectRoot(RemoteDriveComboBox, listing.CurrentPath);
            await UpdateRemoteDriveSpaceAsync(client, listing.CurrentPath);
            _remoteParentPath = listing.ParentPath;
            ReplaceItems(_remoteEntries, listing.Entries);
        }, CrossPlatformText.RemoteBrowseError);
    }

    private void UpdateLocalDriveSpace(string? path)
    {
        try
        {
            var resolvedPath = string.IsNullOrWhiteSpace(path)
                ? (LocalDriveComboBox.SelectedItem as string ?? Path.GetPathRoot(GetDefaultLocalPath()))
                : path;
            if (string.IsNullOrWhiteSpace(resolvedPath))
            {
                LocalDriveSpaceTextBlock.Text = CrossPlatformText.DriveFreeSpaceUnknown;
                return;
            }

            var root = Path.GetPathRoot(resolvedPath);
            if (string.IsNullOrWhiteSpace(root))
            {
                LocalDriveSpaceTextBlock.Text = CrossPlatformText.DriveFreeSpaceUnknown;
                return;
            }

            var drive = new DriveInfo(root);
            LocalDriveSpaceTextBlock.Text = drive.IsReady
                ? CrossPlatformText.DriveFreeSpace(FormatByteSize(drive.AvailableFreeSpace), FormatByteSize(drive.TotalSize))
                : CrossPlatformText.DriveFreeSpaceUnknown;
        }
        catch
        {
            LocalDriveSpaceTextBlock.Text = CrossPlatformText.DriveFreeSpaceUnknown;
        }
    }

    private async Task UpdateRemoteDriveSpaceAsync(TeacherApiClient client, string? path)
    {
        try
        {
            var space = await client.GetRemoteDriveSpaceAsync(path);
            RemoteDriveSpaceTextBlock.Text = space is null
                ? CrossPlatformText.DriveFreeSpaceUnknown
                : CrossPlatformText.DriveFreeSpace(FormatByteSize(space.AvailableBytes), FormatByteSize(space.TotalBytes));
        }
        catch
        {
            RemoteDriveSpaceTextBlock.Text = CrossPlatformText.DriveFreeSpaceUnknown;
        }
    }

    private async void UploadButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (LocalFilesGrid.SelectedItem is not FileSystemEntryDto entry || entry.IsDirectory)
        {
            SetStatus(CrossPlatformText.ChooseLocalFileToUpload);
            return;
        }

        await RunBusyAsync(async () =>
        {
            var client = CreateClient();
            var progress = new Progress<TeacherApiClient.TransferProgress>(value =>
                SetStatus(BuildTransferStatus(CrossPlatformText.UploadArrow, entry.Name, value)));
            await client.UploadFileAsync(entry.FullPath, RemotePathTextBox.Text ?? string.Empty, progress);
            await LoadRemoteDirectoryAsync(RemotePathTextBox.Text);
            SetStatus(CrossPlatformText.Uploaded(entry.Name));
        }, CrossPlatformText.UploadError);
    }

    private async void SendToSelectedStudentsButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var selectedAgents = GetSelectedAgents();
        if (selectedAgents.Count == 0)
        {
            SetStatus(CrossPlatformText.ChooseAgentsForDistribution);
            return;
        }

        await DistributeLocalSelectionAsync(selectedAgents);
    }

    private async void SendToAllOnlineStudentsButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var targetAgents = _allAgents
            .Where(x => string.Equals(x.Status, CrossPlatformText.Online, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (targetAgents.Count == 0)
        {
            SetStatus(CrossPlatformText.NoOnlineAgentsAvailableForDistribution);
            return;
        }

        await DistributeLocalSelectionAsync(targetAgents);
    }

    private async void ClearSelectedFolderSelectedMenuItem_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var selectedAgents = GetSelectedAgents();
        if (selectedAgents.Count == 0)
        {
            SetStatus(CrossPlatformText.ChooseAgentsForDistribution);
            return;
        }

        await ClearSelectedRemoteDirectoryAsync(selectedAgents, allOnline: false);
    }

    private async void ClearSelectedFolderAllMenuItem_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var targetAgents = _allAgents
            .Where(x => string.Equals(x.Status, CrossPlatformText.Online, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (targetAgents.Count == 0)
        {
            SetStatus(CrossPlatformText.NoOnlineAgentsAvailableForGroupCommand);
            return;
        }

        await ClearSelectedRemoteDirectoryAsync(targetAgents, allOnline: true);
    }

    private async void LockBrowsersAllMenuItem_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var targetAgents = _allAgents
            .Where(x => string.Equals(x.Status, CrossPlatformText.Online, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (targetAgents.Count == 0)
        {
            SetStatus(CrossPlatformText.NoOnlineAgentsAvailableForGroupCommand);
            return;
        }

        await SetBrowserLockOnAgentsAsync(targetAgents, enabled: true);
    }

    private async void LockInputAllMenuItem_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var targetAgents = _allAgents
            .Where(x => string.Equals(x.Status, CrossPlatformText.Online, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (targetAgents.Count == 0)
        {
            SetStatus(CrossPlatformText.NoOnlineAgentsAvailableForGroupCommand);
            return;
        }

        await SetInputLockOnAgentsAsync(targetAgents, enabled: true);
    }

    private async void UnlockInputAllMenuItem_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var targetAgents = _allAgents
            .Where(x => string.Equals(x.Status, CrossPlatformText.Online, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (targetAgents.Count == 0)
        {
            SetStatus(CrossPlatformText.NoOnlineAgentsAvailableForGroupCommand);
            return;
        }

        await SetInputLockOnAgentsAsync(targetAgents, enabled: false);
    }

    private async void RunCommandSelectedMenuItem_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var targetAgents = GetSelectedAgents();
        if (targetAgents.Count == 0)
        {
            SetStatus(CrossPlatformText.ChooseAgentsForDistribution);
            return;
        }

        await ExecuteRemoteCommandOnAgentsAsync(targetAgents, selectedOnly: true);
    }

    private async void RunCommandAllMenuItem_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var targetAgents = _allAgents
            .Where(x => string.Equals(x.Status, CrossPlatformText.Online, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (targetAgents.Count == 0)
        {
            SetStatus(CrossPlatformText.NoOnlineAgentsAvailableForGroupCommand);
            return;
        }

        await ExecuteRemoteCommandOnAgentsAsync(targetAgents, selectedOnly: false);
    }

    private async void UpdateSelectedMenuItem_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var targetAgents = GetSelectedAgents();
        if (targetAgents.Count == 0)
        {
            SetStatus(CrossPlatformText.ChooseAgentsForDistribution);
            return;
        }

        await StartAgentUpdateOnAgentsAsync(targetAgents, selectedOnly: true);
    }

    private async void UpdateAllMenuItem_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var targetAgents = _allAgents
            .Where(x => string.Equals(x.Status, CrossPlatformText.Online, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (targetAgents.Count == 0)
        {
            SetStatus(CrossPlatformText.NoOnlineAgentsAvailableForGroupCommand);
            return;
        }

        await StartAgentUpdateOnAgentsAsync(targetAgents, selectedOnly: false);
    }

    private async void RefreshFrequentProgramsMenuItem_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await RefreshFrequentProgramsAsync();
    }

    private async void ManageFrequentProgramsMenuItem_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var entries = await FrequentProgramsWindow.ShowAsync(this, _frequentPrograms);
        if (entries is null)
        {
            return;
        }

        _frequentPrograms = entries.ToList();
        _frequentProgramStore.Save(_frequentPrograms);
        SetStatus(CrossPlatformText.FrequentProgramsRefreshed(_frequentPrograms.Count));
    }

    private async void ShutdownSelectedMenuItem_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var targetAgents = GetSelectedAgents();
        if (targetAgents.Count == 0)
        {
            SetStatus(CrossPlatformText.ChooseAgentsForDistribution);
            return;
        }

        await ExecutePowerActionOnAgentsAsync(targetAgents, PowerActionKind.Shutdown, selectedOnly: true);
    }

    private async void RestartSelectedMenuItem_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var targetAgents = GetSelectedAgents();
        if (targetAgents.Count == 0)
        {
            SetStatus(CrossPlatformText.ChooseAgentsForDistribution);
            return;
        }

        await ExecutePowerActionOnAgentsAsync(targetAgents, PowerActionKind.Restart, selectedOnly: true);
    }

    private async void LogOffSelectedMenuItem_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var targetAgents = GetSelectedAgents();
        if (targetAgents.Count == 0)
        {
            SetStatus(CrossPlatformText.ChooseAgentsForDistribution);
            return;
        }

        await ExecutePowerActionOnAgentsAsync(targetAgents, PowerActionKind.LogOff, selectedOnly: true);
    }

    private async void ShutdownAllMenuItem_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var targetAgents = _allAgents.Where(x => string.Equals(x.Status, CrossPlatformText.Online, StringComparison.OrdinalIgnoreCase)).ToList();
        if (targetAgents.Count == 0)
        {
            SetStatus(CrossPlatformText.NoOnlineAgentsAvailableForGroupCommand);
            return;
        }

        await ExecutePowerActionOnAgentsAsync(targetAgents, PowerActionKind.Shutdown, selectedOnly: false);
    }

    private async void RestartAllMenuItem_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var targetAgents = _allAgents.Where(x => string.Equals(x.Status, CrossPlatformText.Online, StringComparison.OrdinalIgnoreCase)).ToList();
        if (targetAgents.Count == 0)
        {
            SetStatus(CrossPlatformText.NoOnlineAgentsAvailableForGroupCommand);
            return;
        }

        await ExecutePowerActionOnAgentsAsync(targetAgents, PowerActionKind.Restart, selectedOnly: false);
    }

    private async void LogOffAllMenuItem_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var targetAgents = _allAgents.Where(x => string.Equals(x.Status, CrossPlatformText.Online, StringComparison.OrdinalIgnoreCase)).ToList();
        if (targetAgents.Count == 0)
        {
            SetStatus(CrossPlatformText.NoOnlineAgentsAvailableForGroupCommand);
            return;
        }

        await ExecutePowerActionOnAgentsAsync(targetAgents, PowerActionKind.LogOff, selectedOnly: false);
    }

    private async void CollectStudentWorkSelectedMenuItem_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var selectedAgents = GetSelectedAgents();
        if (selectedAgents.Count == 0)
        {
            SetStatus(CrossPlatformText.ChooseAgentsForDistribution);
            return;
        }

        await CollectStudentWorkAsync(selectedAgents);
    }

    private async void CollectStudentWorkAllMenuItem_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var targetAgents = _allAgents
            .Where(x => string.Equals(x.Status, CrossPlatformText.Online, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (targetAgents.Count == 0)
        {
            SetStatus(CrossPlatformText.NoOnlineAgentsAvailableForGroupCommand);
            return;
        }

        await CollectStudentWorkAsync(targetAgents);
    }

    private async void CreateStudentWorkFolderAllMenuItem_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _preparedStudentWorkFolders.Clear();
        await EnsureStudentWorkFolderOnAvailableAgentsAsync(reportSummary: true);
    }

    private async void CollectStudentWorkToTeacherPcMenuItem_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var targetAgents = _allAgents
            .Where(x => string.Equals(x.Status, CrossPlatformText.Online, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (targetAgents.Count == 0)
        {
            SetStatus(CrossPlatformText.NoOnlineAgentsAvailableForGroupCommand);
            return;
        }

        await CollectStudentWorkAsync(targetAgents);
    }

    private async void ClearStudentWorkFolderAllMenuItem_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var targetAgents = _allAgents
            .Where(x => string.Equals(x.Status, CrossPlatformText.Online, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (targetAgents.Count == 0)
        {
            SetStatus(CrossPlatformText.NoOnlineAgentsAvailableForGroupCommand);
            return;
        }

        await ClearConfiguredStudentWorkDirectoryAsync(targetAgents);
    }

    private async void DownloadButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (RemoteFilesGrid.SelectedItem is not FileSystemEntryDto entry || entry.IsDirectory)
        {
            SetStatus(CrossPlatformText.ChooseRemoteFileToDownload);
            return;
        }

        await RunBusyAsync(async () =>
        {
            var client = CreateClient();
            var progress = new Progress<TeacherApiClient.TransferProgress>(value =>
                SetStatus(BuildTransferStatus(CrossPlatformText.DownloadArrow, entry.Name, value)));
            await client.DownloadRemoteFileAsync(entry.FullPath, LocalPathTextBox.Text ?? GetDefaultLocalPath(), progress);
            await LoadLocalDirectoryAsync(LocalPathTextBox.Text);
            SetStatus(CrossPlatformText.Downloaded(entry.Name));
        }, CrossPlatformText.DownloadError);
    }

    private async void OpenRemoteButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (RemoteFilesGrid.SelectedItem is not FileSystemEntryDto entry)
        {
            SetStatus(CrossPlatformText.ChooseRemoteEntryFirst);
            return;
        }

        await RunBusyAsync(async () =>
        {
            var client = CreateClient();
            await client.OpenRemoteEntryAsync(entry.FullPath);
            SetStatus(CrossPlatformText.OpenedRemote(entry.Name));
        }, CrossPlatformText.OpenRemoteError);
    }

    private void OpenLocalButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (LocalFilesGrid.SelectedItem is not FileSystemEntryDto entry)
        {
            SetStatus(CrossPlatformText.ChooseLocalEntryFirst);
            return;
        }

        OpenLocalEntry(entry);
    }

    private async void RenameLocalButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (LocalFilesGrid.SelectedItem is not FileSystemEntryDto entry)
        {
            SetStatus(CrossPlatformText.ChooseLocalEntryFirst);
            return;
        }

        var newName = await TextInputDialog.ShowAsync(this, CrossPlatformText.RenameLocalEntryTitle, CrossPlatformText.EntryName, entry.Name);
        if (string.IsNullOrWhiteSpace(newName))
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            RenameLocalEntry(entry, newName);
            await LoadLocalDirectoryAsync(LocalPathTextBox.Text);
            SetStatus(CrossPlatformText.RenamedLocalEntry(entry.Name, newName.Trim()));
        }, CrossPlatformText.LocalRenameError);
    }

    private async void RenameRemoteButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (RemoteFilesGrid.SelectedItem is not FileSystemEntryDto entry)
        {
            SetStatus(CrossPlatformText.ChooseRemoteEntryFirst);
            return;
        }

        var newName = await TextInputDialog.ShowAsync(this, CrossPlatformText.RenameRemoteEntryTitle, CrossPlatformText.EntryName, entry.Name);
        if (string.IsNullOrWhiteSpace(newName))
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            var client = CreateClient();
            await client.RenameRemoteEntryAsync(entry.FullPath, newName.Trim());
            await LoadRemoteDirectoryAsync(RemotePathTextBox.Text);
            SetStatus(CrossPlatformText.RenamedRemoteEntry(entry.Name, newName.Trim()));
        }, CrossPlatformText.RemoteRenameError);
    }

    private async void DeleteLocalButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (LocalFilesGrid.SelectedItem is not FileSystemEntryDto entry)
        {
            SetStatus(CrossPlatformText.ChooseLocalEntryFirst);
            return;
        }

        if (!await ConfirmationDialog.ShowAsync(this, CrossPlatformText.DeleteLocalEntryTitle, CrossPlatformText.DeleteLocalEntryPrompt(entry.Name)))
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            if (entry.IsDirectory)
            {
                Directory.Delete(entry.FullPath, recursive: true);
            }
            else
            {
                File.Delete(entry.FullPath);
            }

            await LoadLocalDirectoryAsync(LocalPathTextBox.Text);
            SetStatus(CrossPlatformText.DeletedLocalEntry(entry.Name));
        }, CrossPlatformText.LocalDeleteError);
    }

    private async void DeleteRemoteButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (RemoteFilesGrid.SelectedItem is not FileSystemEntryDto entry)
        {
            SetStatus(CrossPlatformText.ChooseRemoteEntryFirst);
            return;
        }

        if (!await ConfirmationDialog.ShowAsync(this, CrossPlatformText.DeleteRemoteEntryTitle, CrossPlatformText.DeleteRemoteEntryPrompt(entry.Name)))
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            var client = CreateClient();
            await client.DeleteRemoteEntryAsync(entry.FullPath);
            await LoadRemoteDirectoryAsync(RemotePathTextBox.Text);
            SetStatus(CrossPlatformText.DeletedRemoteEntry(entry.Name));
        }, CrossPlatformText.RemoteDeleteError);
    }

    private async void NewRemoteFolderButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var folderName = await TextInputDialog.ShowAsync(this, CrossPlatformText.CreateRemoteFolderTitle, CrossPlatformText.FolderName, CrossPlatformText.DefaultFolderName);
        if (string.IsNullOrWhiteSpace(folderName))
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            var client = CreateClient();
            await client.CreateRemoteDirectoryAsync(RemotePathTextBox.Text ?? string.Empty, folderName);
            await LoadRemoteDirectoryAsync(RemotePathTextBox.Text);
            SetStatus(CrossPlatformText.CreatedRemoteFolder(folderName));
        }, CrossPlatformText.CreateFolderError);
    }

    private async void UpLocalButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var current = LocalPathTextBox.Text;
        if (string.IsNullOrWhiteSpace(current))
        {
            return;
        }

        var parent = Directory.GetParent(current)?.FullName;
        if (!string.IsNullOrWhiteSpace(parent))
        {
            await LoadLocalDirectoryAsync(parent);
        }
    }

    private async void UpRemoteButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_remoteParentPath))
        {
            await LoadRemoteDirectoryAsync(_remoteParentPath);
        }
    }

    private async void LocalDriveComboBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressDriveSelection || LocalDriveComboBox.SelectedItem is not string root || string.IsNullOrWhiteSpace(root))
        {
            return;
        }

        await LoadLocalDirectoryAsync(root);
    }

    private async void RemoteDriveComboBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressDriveSelection || RemoteDriveComboBox.SelectedItem is not string root || string.IsNullOrWhiteSpace(root))
        {
            return;
        }

        await LoadRemoteDirectoryAsync(root);
    }

    private async void AgentsGrid_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        await ConnectSelectedAgentAsync();
    }

    private async void LocalFilesGrid_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (LocalFilesGrid.SelectedItem is not FileSystemEntryDto entry)
        {
            return;
        }

        if (entry.IsDirectory)
        {
            await LoadLocalDirectoryAsync(entry.FullPath);
            return;
        }

        OpenLocalEntry(entry);
    }

    private async void RemoteFilesGrid_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (RemoteFilesGrid.SelectedItem is FileSystemEntryDto entry && entry.IsDirectory)
        {
            await LoadRemoteDirectoryAsync(entry.FullPath);
        }
    }

    private async void AboutMenuItem_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var aboutWindow = new AboutWindow();
        await aboutWindow.ShowDialog(this);
    }

    private async void CheckClientUpdateMenuItem_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var dialog = new ClientUpdateWindow(_clientUpdateService);
        await dialog.ShowDialog(this);
    }

    private async Task DistributeLocalSelectionAsync(IReadOnlyList<DiscoveredAgentRow> targetAgents)
    {
        if (LocalFilesGrid.SelectedItem is not FileSystemEntryDto entry)
        {
            SetStatus(CrossPlatformText.ChooseLocalFileOrFolderToDistribute);
            return;
        }

        var destinationRoot = GetConfiguredDistributionDestinationPath();
        if (string.IsNullOrWhiteSpace(destinationRoot))
        {
            SetStatus(CrossPlatformText.DistributionDestinationPathRequired);
            return;
        }

        SetStatus(CrossPlatformText.PreparingDistributionPlan);
        var plan = LocalDistributionPlanner.Build(entry, destinationRoot);

        var failures = new List<string>();
        var succeeded = 0;

        await RunBusyAsync(async () =>
        {
            for (var agentIndex = 0; agentIndex < targetAgents.Count; agentIndex++)
            {
                var agent = targetAgents[agentIndex];
                try
                {
                    var client = new TeacherApiClient($"http://{agent.RespondingAddress}:{agent.Port}", _clientSettings.SharedSecret);
                    await CopyEntryToAgentAsync(
                        client,
                        agent,
                        plan,
                        agentIndex + 1,
                        targetAgents.Count,
                        SetStatus);
                    succeeded++;
                }
                catch (Exception ex)
                {
                    failures.Add($"{agent.MachineName}: {ex.Message}");
                }
            }

            if (!string.IsNullOrWhiteSpace(_lastConnectedServerUrl) &&
                targetAgents.Any(x => string.Equals($"http://{x.RespondingAddress}:{x.Port}", _lastConnectedServerUrl, StringComparison.OrdinalIgnoreCase)))
            {
                await LoadRemoteDirectoryAsync(RemotePathTextBox.Text);
            }

            SetStatus(failures.Count == 0
                ? CrossPlatformText.DistributionCompleted(entry.Name, succeeded)
                : CrossPlatformText.DistributionCompletedWithFailures(entry.Name, succeeded, failures.Count));

            if (failures.Count > 0)
            {
                await ConfirmationDialog.ShowInfoAsync(
                    this,
                    CrossPlatformText.BulkCopyResultTitle,
                    string.Join(Environment.NewLine, failures));
            }
        }, CrossPlatformText.BulkCopyError);
    }

    private async Task ClearSelectedRemoteDirectoryAsync(IReadOnlyList<DiscoveredAgentRow> targetAgents, bool allOnline)
    {
        var destinationRoot = GetConfiguredDistributionDestinationPath();
        if (string.IsNullOrWhiteSpace(destinationRoot))
        {
            SetStatus(CrossPlatformText.ClearDestinationFolderNotConfigured);
            return;
        }

        if (!await ConfirmationDialog.ShowAsync(
                this,
                CrossPlatformText.GroupCommandsTitle,
                CrossPlatformText.ClearDirectoryPrompt(destinationRoot, targetAgents.Count, allOnline)))
        {
            return;
        }

        var failures = new List<string>();
        var succeeded = 0;

        await RunBusyAsync(async () =>
        {
            for (var agentIndex = 0; agentIndex < targetAgents.Count; agentIndex++)
            {
                var agent = targetAgents[agentIndex];
                try
                {
                    SetStatus(CrossPlatformText.ClearingDirectoryProgress(agent.MachineName, destinationRoot, agentIndex + 1, targetAgents.Count));
                    var client = new TeacherApiClient($"http://{agent.RespondingAddress}:{agent.Port}", _clientSettings.SharedSecret);
                    await client.EnsureSharedWritableDirectoryAsync(destinationRoot);
                    await client.ClearRemoteDirectoryAsync(destinationRoot);
                    succeeded++;
                }
                catch (Exception ex)
                {
                    failures.Add($"{agent.MachineName}: {ex.Message}");
                }
            }

            if (!string.IsNullOrWhiteSpace(_lastConnectedServerUrl) &&
                targetAgents.Any(x => string.Equals($"http://{x.RespondingAddress}:{x.Port}", _lastConnectedServerUrl, StringComparison.OrdinalIgnoreCase)))
            {
                await LoadRemoteDirectoryAsync(RemotePathTextBox.Text);
            }

            SetStatus(failures.Count == 0
                ? CrossPlatformText.ClearDirectoryCompleted(destinationRoot, succeeded)
                : CrossPlatformText.ClearDirectoryCompletedWithFailures(destinationRoot, succeeded, failures.Count));

            if (failures.Count > 0)
            {
                await ConfirmationDialog.ShowInfoAsync(
                    this,
                    CrossPlatformText.BulkCommandsResultTitle,
                    string.Join(Environment.NewLine, failures));
            }
        }, CrossPlatformText.BulkClearError);
    }

    private async Task SetBrowserLockOnAgentsAsync(IReadOnlyList<DiscoveredAgentRow> targetAgents, bool enabled)
    {
        if (!await ConfirmationDialog.ShowAsync(
                this,
                CrossPlatformText.GroupCommandsTitle,
                CrossPlatformText.BrowserLockPrompt(targetAgents.Count)))
        {
            return;
        }

        var failures = new List<string>();
        var succeeded = 0;

        await RunBusyAsync(async () =>
        {
            for (var agentIndex = 0; agentIndex < targetAgents.Count; agentIndex++)
            {
                var agent = targetAgents[agentIndex];
                try
                {
                    SetStatus(CrossPlatformText.BrowserLockProgress(agent.MachineName, agentIndex + 1, targetAgents.Count));
                    var client = new TeacherApiClient($"http://{agent.RespondingAddress}:{agent.Port}", _clientSettings.SharedSecret);
                    await client.SetBrowserLockEnabledAsync(enabled);
                    ReplaceAgentRow(agent with { BrowserLockEnabled = enabled });
                    succeeded++;
                }
                catch (Exception ex)
                {
                    failures.Add($"{agent.MachineName}: {ex.Message}");
                }
            }

            SetStatus(failures.Count == 0
                ? CrossPlatformText.BrowserLockCompleted(succeeded)
                : CrossPlatformText.BrowserLockCompletedWithFailures(succeeded, failures.Count));

            if (failures.Count > 0)
            {
                await ConfirmationDialog.ShowInfoAsync(
                    this,
                    CrossPlatformText.BulkCommandsResultTitle,
                    string.Join(Environment.NewLine, failures));
            }
        }, CrossPlatformText.BulkBrowserLockError);
    }

    private async Task SetInputLockOnAgentsAsync(IReadOnlyList<DiscoveredAgentRow> targetAgents, bool enabled)
    {
        if (!await ConfirmationDialog.ShowAsync(
                this,
                CrossPlatformText.GroupCommandsTitle,
                CrossPlatformText.InputLockPrompt(targetAgents.Count, enabled)))
        {
            return;
        }

        var failures = new List<string>();
        var succeeded = 0;

        await RunBusyAsync(async () =>
        {
            for (var agentIndex = 0; agentIndex < targetAgents.Count; agentIndex++)
            {
                var agent = targetAgents[agentIndex];
                try
                {
                    SetStatus(CrossPlatformText.InputLockProgress(agent.MachineName, agentIndex + 1, targetAgents.Count, enabled));
                    var client = new TeacherApiClient($"http://{agent.RespondingAddress}:{agent.Port}", _clientSettings.SharedSecret);
                    await client.SetInputLockEnabledAsync(enabled);
                    ReplaceAgentRow(agent with { InputLockEnabled = enabled });
                    succeeded++;
                }
                catch (Exception ex)
                {
                    failures.Add($"{agent.MachineName}: {ex.Message}");
                }
            }

            SetStatus(failures.Count == 0
                ? CrossPlatformText.InputLockCompleted(succeeded, enabled)
                : CrossPlatformText.InputLockCompletedWithFailures(succeeded, failures.Count, enabled));

            if (failures.Count > 0)
            {
                await ConfirmationDialog.ShowInfoAsync(
                    this,
                    CrossPlatformText.BulkCommandsResultTitle,
                    string.Join(Environment.NewLine, failures));
            }
        }, CrossPlatformText.BulkInputLockError);
    }

    private async Task ExecutePowerActionOnAgentsAsync(IReadOnlyList<DiscoveredAgentRow> targetAgents, PowerActionKind action, bool selectedOnly)
    {
        if (!await ConfirmationDialog.ShowAsync(
                this,
                CrossPlatformText.GroupCommandsTitle,
                CrossPlatformText.PowerActionPrompt(action, targetAgents.Count, selectedOnly)))
        {
            return;
        }

        var failures = new List<string>();
        var succeeded = 0;

        await RunBusyAsync(async () =>
        {
            for (var agentIndex = 0; agentIndex < targetAgents.Count; agentIndex++)
            {
                var agent = targetAgents[agentIndex];
                try
                {
                    SetStatus(CrossPlatformText.PowerActionProgress(action, agent.MachineName, agentIndex + 1, targetAgents.Count));
                    var client = new TeacherApiClient($"http://{agent.RespondingAddress}:{agent.Port}", _clientSettings.SharedSecret);
                    await client.ExecutePowerActionAsync(action);
                    succeeded++;
                }
                catch (Exception ex)
                {
                    failures.Add($"{agent.MachineName}: {ex.Message}");
                }
            }

            SetStatus(failures.Count == 0
                ? CrossPlatformText.PowerActionCompleted(action, succeeded)
                : CrossPlatformText.PowerActionCompletedWithFailures(action, succeeded, failures.Count));

            if (failures.Count > 0)
            {
                await ConfirmationDialog.ShowInfoAsync(
                    this,
                    CrossPlatformText.BulkCommandsResultTitle,
                    string.Join(Environment.NewLine, failures));
            }
        }, CrossPlatformText.BulkPowerActionError(action));
    }

    private async Task StartAgentUpdateOnAgentsAsync(IReadOnlyList<DiscoveredAgentRow> targetAgents, bool selectedOnly)
    {
        if (!await ConfirmationDialog.ShowAsync(
                this,
                CrossPlatformText.GroupCommandsTitle,
                CrossPlatformText.BulkAgentUpdatePrompt(targetAgents.Count, selectedOnly)))
        {
            return;
        }

        var failures = new List<string>();
        var succeeded = 0;

        await RunBusyAsync(async () =>
        {
            for (var agentIndex = 0; agentIndex < targetAgents.Count; agentIndex++)
            {
                var agent = targetAgents[agentIndex];
                try
                {
                    SetStatus(CrossPlatformText.BulkAgentUpdateProgress(agent.MachineName, agentIndex + 1, targetAgents.Count));
                    var client = new TeacherApiClient($"http://{agent.RespondingAddress}:{agent.Port}", _clientSettings.SharedSecret);
                    var request = await CreatePreparedUpdateRequestAsync(agent, client);
                    if (request is null)
                    {
                        throw new InvalidOperationException(CrossPlatformText.UpdatePreparationMissing);
                    }
                    var status = await client.StartAgentUpdateAsync(request);
                    if (status is not null)
                    {
                        ReplaceAgentRow(ApplyUpdateStatus(agent, status));
                    }
                    succeeded++;
                }
                catch (Exception ex)
                {
                    failures.Add($"{agent.MachineName}: {ex.Message}");
                }
            }

            SetStatus(failures.Count == 0
                ? CrossPlatformText.BulkAgentUpdateCompleted(succeeded)
                : CrossPlatformText.BulkAgentUpdateCompletedWithFailures(succeeded, failures.Count));

            if (failures.Count > 0)
            {
                await ConfirmationDialog.ShowInfoAsync(
                    this,
                    CrossPlatformText.BulkCommandsResultTitle,
                    string.Join(Environment.NewLine, failures));
            }
        }, CrossPlatformText.AgentUpdateStartFailed);
    }

    private async Task EnsureStudentWorkFolderOnAvailableAgentsAsync(bool reportSummary, IReadOnlyList<DiscoveredAgentRow>? overrideTargets = null)
    {
        var studentWorkPath = GetConfiguredStudentWorkPath();
        if (string.IsNullOrWhiteSpace(studentWorkPath))
        {
            return;
        }

        var targetAgents = (overrideTargets ?? _allAgents
                .Where(x => string.Equals(x.Status, CrossPlatformText.Online, StringComparison.OrdinalIgnoreCase))
                .ToList())
            .ToList();

        if (targetAgents.Count == 0)
        {
            return;
        }

        var failures = new List<string>();
        var succeeded = 0;

        foreach (var agent in targetAgents)
        {
            var cacheKey = $"{agent.AgentId}|{studentWorkPath}";
            if (!reportSummary && _preparedStudentWorkFolders.Contains(cacheKey))
            {
                continue;
            }

            try
            {
                var client = new TeacherApiClient($"http://{agent.RespondingAddress}:{agent.Port}", _clientSettings.SharedSecret);
                await client.EnsureSharedWritableDirectoryAsync(studentWorkPath);
                _preparedStudentWorkFolders.Add(cacheKey);
                succeeded++;
            }
            catch (Exception ex)
            {
                if (reportSummary)
                {
                    failures.Add($"{agent.MachineName}: {ex.Message}");
                }
            }
        }

        if (!reportSummary)
        {
            return;
        }

        SetStatus(failures.Count == 0
            ? CrossPlatformText.WorkFolderProvisioned(succeeded)
            : CrossPlatformText.WorkFolderProvisionedWithFailures(succeeded, failures.Count));

        if (failures.Count > 0)
        {
            await ConfirmationDialog.ShowInfoAsync(
                this,
                CrossPlatformText.BulkCommandsResultTitle,
                string.Join(Environment.NewLine, failures));
        }
    }

    private async Task ClearConfiguredStudentWorkDirectoryAsync(IReadOnlyList<DiscoveredAgentRow> targetAgents)
    {
        var studentWorkPath = GetConfiguredStudentWorkPath();
        if (string.IsNullOrWhiteSpace(studentWorkPath))
        {
            SetStatus(CrossPlatformText.StudentWorkFolderNotConfigured);
            return;
        }

        await EnsureStudentWorkFolderOnAvailableAgentsAsync(reportSummary: false, overrideTargets: targetAgents);

        var confirmed = await ConfirmationDialog.ShowAsync(
            this,
            CrossPlatformText.GroupCommandsTitle,
            CrossPlatformText.ClearDirectoryPrompt(studentWorkPath, targetAgents.Count, allOnline: true));

        if (!confirmed)
        {
            return;
        }

        var succeeded = 0;
        var failed = 0;

        try
        {
            for (var index = 0; index < targetAgents.Count; index++)
            {
                var agent = targetAgents[index];
                SetStatus(CrossPlatformText.ClearingDirectoryProgress(agent.MachineName, studentWorkPath, index + 1, targetAgents.Count));

                try
                {
                    var client = new TeacherApiClient($"http://{agent.RespondingAddress}:{agent.Port}", _clientSettings.SharedSecret);
                    await client.ClearRemoteDirectoryAsync(studentWorkPath);
                    succeeded++;
                }
                catch
                {
                    failed++;
                }
            }

            SetStatus(
                failed == 0
                    ? CrossPlatformText.ClearDirectoryCompleted(_clientSettings.StudentWorkFolderName, succeeded)
                    : CrossPlatformText.ClearDirectoryCompletedWithFailures(_clientSettings.StudentWorkFolderName, succeeded, failed));
        }
        catch (Exception ex)
        {
            SetStatus($"{CrossPlatformText.BulkClearError}: {ex.Message}");
        }
    }

    private async Task CollectStudentWorkAsync(IReadOnlyList<DiscoveredAgentRow> targetAgents)
    {
        var studentWorkPath = GetConfiguredStudentWorkPath();
        if (string.IsNullOrWhiteSpace(studentWorkPath))
        {
            SetStatus(CrossPlatformText.StudentWorkFolderNotConfigured);
            return;
        }

        var localDestinationRoot = string.IsNullOrWhiteSpace(LocalPathTextBox.Text)
            ? GetDefaultLocalPath()
            : LocalPathTextBox.Text!;

        Directory.CreateDirectory(localDestinationRoot);
        SetStatus(CrossPlatformText.PreparingWorkCollection);
        await EnsureStudentWorkFolderOnAvailableAgentsAsync(reportSummary: false, overrideTargets: targetAgents);

        var failures = new List<string>();
        var succeeded = 0;

        await RunBusyAsync(async () =>
        {
            for (var agentIndex = 0; agentIndex < targetAgents.Count; agentIndex++)
            {
                var agent = targetAgents[agentIndex];
                try
                {
                    SetStatus(CrossPlatformText.CollectingWorkProgress(agent.MachineName, studentWorkPath, agentIndex + 1, targetAgents.Count));
                    var client = new TeacherApiClient($"http://{agent.RespondingAddress}:{agent.Port}", _clientSettings.SharedSecret);
                    var machineFolder = Path.Combine(localDestinationRoot, SanitizeLocalFolderName(agent.MachineName));
                    var localWorkFolder = Path.Combine(machineFolder, _clientSettings.StudentWorkFolderName);
                    Directory.CreateDirectory(localWorkFolder);
                    await DownloadRemoteDirectoryContentsAsync(client, studentWorkPath, localWorkFolder, SetStatus, agent.MachineName);
                    succeeded++;
                }
                catch (Exception ex)
                {
                    failures.Add($"{agent.MachineName}: {ex.Message}");
                }
            }

            SetStatus(failures.Count == 0
                ? CrossPlatformText.WorkCollectionCompleted(succeeded, localDestinationRoot)
                : CrossPlatformText.WorkCollectionCompletedWithFailures(succeeded, failures.Count, localDestinationRoot));

            if (failures.Count > 0)
            {
                await ConfirmationDialog.ShowInfoAsync(
                    this,
                    CrossPlatformText.BulkCommandsResultTitle,
                    string.Join(Environment.NewLine, failures));
            }
        }, CrossPlatformText.BulkCollectError);
    }

    private static async Task CopyEntryToAgentAsync(
        TeacherApiClient client,
        DiscoveredAgentRow agent,
        LocalDistributionPlan plan,
        int agentIndex,
        int agentCount,
        Action<string> reportStatus,
        CancellationToken cancellationToken = default)
    {
        await client.EnsureRemoteDirectoryPathAsync(plan.DestinationRoot, cancellationToken);
        foreach (var directory in plan.DirectoriesToEnsure)
        {
            await client.EnsureRemoteDirectoryPathAsync(directory, cancellationToken);
        }

        for (var fileIndex = 0; fileIndex < plan.Files.Count; fileIndex++)
        {
            var file = plan.Files[fileIndex];
            reportStatus(CrossPlatformText.DistributionProgress(
                agent.MachineName,
                file.DisplayPath,
                agentIndex,
                agentCount,
                fileIndex + 1,
                plan.Files.Count));
            var progress = new Progress<TeacherApiClient.TransferProgress>(value =>
                reportStatus(BuildBulkTransferStatus(
                    CrossPlatformText.UploadArrow,
                    agent.MachineName,
                    file.DisplayPath,
                    value,
                    agentIndex,
                    agentCount,
                    fileIndex + 1,
                    plan.Files.Count)));
            await client.UploadFileAsync(file.LocalPath, file.RemoteDirectory, progress, cancellationToken);
        }
    }

    private static async Task DownloadRemoteDirectoryContentsAsync(
        TeacherApiClient client,
        string remoteDirectoryPath,
        string localDestinationDirectory,
        Action<string>? reportStatus = null,
        string? agentName = null,
        CancellationToken cancellationToken = default)
    {
        var listing = await client.GetRemoteDirectoryAsync(remoteDirectoryPath, cancellationToken)
                      ?? throw new InvalidOperationException("Remote listing failed.");

        Directory.CreateDirectory(localDestinationDirectory);
        foreach (var entry in listing.Entries)
        {
            if (entry.IsDirectory)
            {
                var childDirectory = Path.Combine(localDestinationDirectory, entry.Name);
                await DownloadRemoteDirectoryContentsAsync(client, entry.FullPath, childDirectory, reportStatus, agentName, cancellationToken);
            }
            else
            {
                var progress = reportStatus is null
                    ? null
                    : new Progress<TeacherApiClient.TransferProgress>(value =>
                        reportStatus(BuildBulkTransferStatus(
                            CrossPlatformText.DownloadArrow,
                            agentName ?? string.Empty,
                            entry.Name,
                            value)));
                await client.DownloadRemoteFileAsync(entry.FullPath, localDestinationDirectory, progress, cancellationToken);
            }
        }
    }

    private void SaveManualAgents()
    {
        _manualAgentStore.Save(_manualAgents);
    }

    private List<DiscoveredAgentRow> GetSelectedAgents()
    {
        var fromItems = AgentsGrid.SelectedItems?
            .OfType<DiscoveredAgentRow>()
            .Distinct()
            .ToList() ?? [];

        if (fromItems.Count > 0)
        {
            return fromItems;
        }

        // Template columns (e.g. lock checkboxes) can absorb clicks so SelectedItems stays empty while
        // SelectedItem still tracks the focused row.
        if (AgentsGrid.SelectedItem is DiscoveredAgentRow single)
        {
            return [single];
        }

        return [];
    }

    private List<DiscoveredAgentRow> FilterOutCurrentConnectedAgent(IEnumerable<DiscoveredAgentRow> agents)
    {
        return agents
            .Where(x => string.Equals(x.Status, CrossPlatformText.Online, StringComparison.OrdinalIgnoreCase))
            .Where(x => string.IsNullOrWhiteSpace(_lastConnectedAgentId)
                || !string.Equals(x.AgentId, _lastConnectedAgentId, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private void ReplaceAgentRow(DiscoveredAgentRow updated)
    {
        var index = _allAgents.FindIndex(x => string.Equals(x.AgentId, updated.AgentId, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            _allAgents[index] = updated;
        }

        ApplyAgentFilters();
    }

    private async Task<StartAgentUpdateRequest?> CreatePreparedUpdateRequestAsync(DiscoveredAgentRow agent, TeacherApiClient client, CancellationToken cancellationToken = default)
    {
        var prepared = _updatePreparationService.GetPreparedUpdate();
        if (prepared is null)
        {
            return null;
        }

        var preferredSource = await _updatePreparationService.BuildPreferredSourceForAgentAsync(agent.RespondingAddress, prepared, cancellationToken);
        return new StartAgentUpdateRequest(
            CheckForUpdatesFirst: false,
            PreferredSource: preferredSource,
            FallbackToConfiguredManifest: false);
    }

    private async Task PollAgentUpdateStatusesAsync()
    {
        var targetAgents = _allAgents
            .Where(x => string.Equals(x.Status, CrossPlatformText.Online, StringComparison.OrdinalIgnoreCase))
            .Where(x => !x.IsManual || !string.IsNullOrWhiteSpace(x.RespondingAddress))
            .ToList();

        foreach (var agent in targetAgents)
        {
            try
            {
                var client = new TeacherApiClient($"http://{agent.RespondingAddress}:{agent.Port}", _clientSettings.SharedSecret);
                var status = await client.GetUpdateStatusAsync();
                if (status is null)
                {
                    continue;
                }

                ReplaceAgentRow(ApplyUpdateStatus(agent, status));
            }
            catch
            {
            }
        }
    }

    private static DiscoveredAgentRow ApplyUpdateStatus(DiscoveredAgentRow agent, AgentUpdateStatusDto status)
    {
        var targetVersion = status.State switch
        {
            AgentUpdateStateKind.Succeeded => status.AvailableVersion ?? agent.Version,
            _ => agent.Version
        };

        return agent with
        {
            Version = targetVersion,
            UpdateStatusBadge = CrossPlatformText.UpdateStateBadge(status),
            UpdateStatusDetail = CrossPlatformText.FormatUpdateStatusDetail(status)
        };
    }

    private static IEnumerable<DiscoveredAgentRow> MergeAgents(
        IEnumerable<DiscoveredAgentRow> manualRows,
        IEnumerable<DiscoveredAgentRow> discoveredRows)
    {
        var merged = new Dictionary<string, DiscoveredAgentRow>(StringComparer.OrdinalIgnoreCase);

        foreach (var manual in manualRows)
        {
            merged[$"manual:{manual.AgentId}"] = manual;
        }

        foreach (var discovered in discoveredRows)
        {
            var existingManual = merged.Values.FirstOrDefault(x =>
                x.IsManual &&
                string.Equals(x.RespondingAddress, discovered.RespondingAddress, StringComparison.OrdinalIgnoreCase) &&
                x.Port == discovered.Port);

            if (existingManual is not null)
            {
                merged[$"manual:{existingManual.AgentId}"] = existingManual with
                {
                    Source = CrossPlatformText.ManualAutoSource,
                    Status = CrossPlatformText.Online,
                    CurrentUser = discovered.CurrentUser,
                    MacAddressesDisplay = string.IsNullOrWhiteSpace(existingManual.MacAddressesDisplay)
                        ? discovered.MacAddressesDisplay
                        : existingManual.MacAddressesDisplay,
                    UpdateStatusBadge = discovered.UpdateStatusBadge,
                    UpdateStatusDetail = discovered.UpdateStatusDetail,
                    Version = discovered.Version,
                    LastSeenUtc = discovered.LastSeenUtc
                };
                continue;
            }

            merged[$"discovered:{discovered.AgentId}"] = discovered;
        }

        return merged.Values
            .OrderBy(x => x.Source, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.MachineName, StringComparer.OrdinalIgnoreCase);
    }

    private static FileSystemEntryDto MapLocalEntry(FileSystemInfo entry)
    {
        return new FileSystemEntryDto(
            entry.Name,
            entry.FullName,
            (entry.Attributes & FileAttributes.Directory) == FileAttributes.Directory,
            entry is FileInfo fileInfo ? fileInfo.Length : null,
            entry.LastWriteTimeUtc)
        {
            AttributesDisplay = FormatAttributes(entry.Attributes)
        };
    }

    private static string FormatAttributes(FileAttributes attributes)
    {
        var values = new List<string>();

        if ((attributes & FileAttributes.Directory) == FileAttributes.Directory)
        {
            values.Add("Dir");
        }

        if ((attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
        {
            values.Add("R");
        }

        if ((attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
        {
            values.Add("H");
        }

        if ((attributes & FileAttributes.System) == FileAttributes.System)
        {
            values.Add("S");
        }

        if ((attributes & FileAttributes.Archive) == FileAttributes.Archive)
        {
            values.Add("A");
        }

        return string.Join(", ", values);
    }

    private async Task RunBusyAsync(Func<Task> operation, string? errorPrefix = null)
    {
        var previousCursor = Cursor;
        try
        {
            Cursor = new Cursor(StandardCursorType.Wait);
            await operation();
        }
        catch (Exception ex)
        {
            SetStatus(errorPrefix is null ? ex.Message : $"{errorPrefix}: {ex.Message}");
        }
        finally
        {
            Cursor = previousCursor;
        }
    }

    private void SetStatus(string text)
    {
        StatusTextBlock.Text = text;
    }

    private static void ReplaceItems<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
    }

    private static string GetDefaultLocalPath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(home) && Directory.Exists(home))
        {
            return home;
        }

        return Directory.GetCurrentDirectory();
    }

    private void PopulateLocalRoots()
    {
        var roots = DriveInfo.GetDrives()
            .Select(x => x.RootDirectory.FullName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (roots.Length == 0)
        {
            roots = [Path.GetPathRoot(GetDefaultLocalPath()) ?? "/"];
        }

        _suppressDriveSelection = true;
        try
        {
            LocalDriveComboBox.ItemsSource = roots;
            if (LocalDriveComboBox.SelectedItem is null && roots.Length > 0)
            {
                LocalDriveComboBox.SelectedItem = roots[0];
            }
        }
        finally
        {
            _suppressDriveSelection = false;
        }
    }

    private async Task PopulateRemoteRootsAsync(TeacherApiClient client)
    {
        var roots = (await client.GetRootsAsync())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        _suppressDriveSelection = true;
        try
        {
            RemoteDriveComboBox.ItemsSource = roots;
            if (RemoteDriveComboBox.SelectedItem is null && roots.Length > 0)
            {
                RemoteDriveComboBox.SelectedItem = roots[0];
            }
        }
        finally
        {
            _suppressDriveSelection = false;
        }
    }

    private void SelectRoot(ComboBox comboBox, string fullPath)
    {
        var root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrWhiteSpace(root))
        {
            return;
        }

        _suppressDriveSelection = true;
        try
        {
            comboBox.SelectedItem = root;
        }
        finally
        {
            _suppressDriveSelection = false;
        }
    }

    private string GetConfiguredStudentWorkPath()
    {
        if (string.IsNullOrWhiteSpace(_clientSettings.StudentWorkRootPath) ||
            string.IsNullOrWhiteSpace(_clientSettings.StudentWorkFolderName))
        {
            return string.Empty;
        }

        return RemoteWindowsPath.Combine(_clientSettings.StudentWorkRootPath, _clientSettings.StudentWorkFolderName);
    }

    private string GetConfiguredDistributionDestinationPath()
    {
        return string.IsNullOrWhiteSpace(_clientSettings.BulkCopyDestinationPath)
            ? string.Empty
            : RemoteWindowsPath.Normalize(_clientSettings.BulkCopyDestinationPath);
    }

    private static string SanitizeLocalFolderName(string rawName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(rawName.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "StudentMachine" : sanitized;
    }

    private static string BuildTransferStatus(string operation, string fileName, TeacherApiClient.TransferProgress progress)
    {
        var transferred = FormatByteSize(progress.BytesTransferred);
        if (!progress.HasTotal)
        {
            return $"{operation} {fileName} ({transferred})";
        }

        return $"{operation} {fileName} ({progress.Percent}% · {transferred} / {FormatByteSize(progress.TotalBytes!.Value)})";
    }

    private static string BuildBulkTransferStatus(
        string operation,
        string agentName,
        string fileName,
        TeacherApiClient.TransferProgress progress,
        int? agentIndex = null,
        int? agentCount = null,
        int? fileIndex = null,
        int? fileCount = null)
    {
        var prefix = string.IsNullOrWhiteSpace(agentName)
            ? operation
            : $"{operation} {agentName}";
        var scope = agentIndex.HasValue && agentCount.HasValue && fileIndex.HasValue && fileCount.HasValue
            ? $" ({agentIndex}/{agentCount}, файл {fileIndex}/{fileCount})"
            : string.Empty;
        var transferred = FormatByteSize(progress.BytesTransferred);

        if (!progress.HasTotal)
        {
            return $"{prefix}{scope} {fileName} ({transferred})";
        }

        return $"{prefix}{scope} {fileName} ({progress.Percent}% · {transferred} / {FormatByteSize(progress.TotalBytes!.Value)})";
    }

    private static string FormatByteSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unitIndex = 0;

        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return unitIndex == 0
            ? $"{value:0} {units[unitIndex]}"
            : $"{value:0.##} {units[unitIndex]}";
    }

    private sealed record DiscoveredAgentRow(
        string AgentId,
        string Source,
        string Status,
        string GroupName,
        string MachineName,
        string CurrentUser,
        string RespondingAddress,
        int Port,
        string MacAddressesDisplay,
        string Notes,
        string UpdateStatusBadge,
        string UpdateStatusDetail,
        string Version,
        bool VncEnabled,
        bool VncRunning,
        bool VncViewOnly,
        int VncPort,
        string VncStatusMessage,
        DateTime LastSeenUtc,
        bool IsManual)
    {
        public bool BrowserLockEnabled { get; set; }
        public bool InputLockEnabled { get; set; }
        public string LastSeenDisplay => LastSeenUtc == DateTime.MinValue ? string.Empty : LastSeenUtc.ToString("u");

        public static DiscoveredAgentRow FromDto(AgentDiscoveryDto dto)
        {
            return new DiscoveredAgentRow(
                dto.AgentId,
                CrossPlatformText.AutoSource,
                CrossPlatformText.Online,
                string.Empty,
                dto.MachineName,
                NormalizeUserDisplay(dto.CurrentUser, dto.MachineName),
                dto.RespondingAddress,
                dto.Port,
                string.Join(", ", dto.MacAddresses),
                string.Empty,
                string.Empty,
                string.Empty,
                dto.Version,
                false,
                false,
                true,
                0,
                string.Empty,
                dto.LastSeenUtc,
                false);
        }

        public static DiscoveredAgentRow FromManualEntry(ManualAgentEntry entry)
        {
            return new DiscoveredAgentRow(
                entry.Id,
                CrossPlatformText.ManualSource,
                CrossPlatformText.Unknown,
                entry.GroupName,
                entry.DisplayName,
                string.Empty,
                entry.IpAddress,
                entry.Port,
                entry.MacAddress,
                entry.Notes,
                string.Empty,
                string.Empty,
                CrossPlatformText.ManualVersion,
                false,
                false,
                true,
                0,
                string.Empty,
                DateTime.MinValue,
                true);
        }
    }

    private string BuildAgentAvailabilityStatus(int discoveredCount, int manualCount)
    {
        return string.IsNullOrWhiteSpace(_lastConnectedMachineName)
            ? CrossPlatformText.MachineSummary(_allAgents.Count, discoveredCount, manualCount)
            : CrossPlatformText.MachineSummaryWithConnected(_allAgents.Count, discoveredCount, manualCount, _lastConnectedMachineName);
    }

    private static string NormalizeUserDisplay(string? currentUser, string machineName)
    {
        if (string.IsNullOrWhiteSpace(currentUser))
        {
            return string.Empty;
        }

        var trimmed = currentUser.Trim();
        var accountName = trimmed.Contains('\\', StringComparison.Ordinal)
            ? trimmed[(trimmed.LastIndexOf('\\') + 1)..]
            : trimmed;

        return string.Equals(accountName, $"{machineName}$", StringComparison.OrdinalIgnoreCase)
            ? string.Empty
            : trimmed;
    }

    private void ApplyLocalization()
    {
        var selectedStatus = StatusFilterComboBox.SelectedItem?.ToString();
        Title = CrossPlatformText.MainTitle;
        ConnectionMenuItem.Header = CrossPlatformText.ConnectionMenu;
        SettingsMenuItem.Header = CrossPlatformText.Settings;
        RefreshAgentsMenuItem.Header = CrossPlatformText.RefreshAgents;
        ConnectSelectedMenuItem.Header = CrossPlatformText.ConnectSelectedAgent;
        DesktopIconsMenuItem.Header = CrossPlatformText.DesktopIconsMenu;
        SaveDesktopIconLayoutMenuItem.Header = CrossPlatformText.SaveDesktopIconLayout;
        RestoreDesktopIconLayoutMenuItem.Header = CrossPlatformText.RestoreDesktopIconLayout;
        CheckAgentUpdateMenuItem.Header = CrossPlatformText.CheckForAgentUpdate;
        StartAgentUpdateMenuItem.Header = CrossPlatformText.StartAgentUpdate;
        AddManualMenuItem.Header = CrossPlatformText.AddManualAgent;
        EditManualMenuItem.Header = CrossPlatformText.EditManualAgent;
        RemoveManualMenuItem.Header = CrossPlatformText.RemoveManualAgent;
        GroupCommandsMenuItem.Header = CrossPlatformText.GroupCommands;
        DestinationFolderMenuItem.Header = CrossPlatformText.DestinationFolderMenu;
        BrowserCommandsMenuItem.Header = CrossPlatformText.BrowserCommandsMenu;
        InputCommandsMenuItem.Header = CrossPlatformText.InputCommandsMenu;
        CommandsMenuItem.Header = CrossPlatformText.CommandsMenu;
        DesktopIconsCommandsMenuItem.Header = CrossPlatformText.DesktopIconsMenu;
        RestoreDesktopIconsSelectedMenuItem.Header = CrossPlatformText.RestoreDesktopIconLayoutOnSelectedStudents;
        RestoreDesktopIconsAllMenuItem.Header = CrossPlatformText.RestoreDesktopIconLayoutOnAllOnlineStudents;
        ApplyCurrentDesktopIconsSelectedMenuItem.Header = CrossPlatformText.ApplyCurrentDesktopIconLayoutToSelectedStudents;
        ApplyCurrentDesktopIconsAllMenuItem.Header = CrossPlatformText.ApplyCurrentDesktopIconLayoutToAllOnlineStudents;
        LockBrowsersAllMenuItem.Header = CrossPlatformText.LockBrowsersOnAllOnlineStudents;
        LockInputAllMenuItem.Header = CrossPlatformText.LockInputOnAllOnlineStudents;
        UnlockInputAllMenuItem.Header = CrossPlatformText.UnlockInputOnAllOnlineStudents;
        RunCommandSelectedMenuItem.Header = CrossPlatformText.RunCommandOnSelectedStudents;
        RunCommandAllMenuItem.Header = CrossPlatformText.RunCommandOnAllOnlineStudents;
        PowerCommandsMenuItem.Header = CrossPlatformText.PowerCommandsMenu;
        SelectedPowerMenuItem.Header = CrossPlatformText.SelectedStudentsMenu;
        AllOnlinePowerMenuItem.Header = CrossPlatformText.AllOnlineStudentsMenu;
        ShutdownSelectedMenuItem.Header = CrossPlatformText.ShutdownCommand;
        RestartSelectedMenuItem.Header = CrossPlatformText.RestartCommand;
        LogOffSelectedMenuItem.Header = CrossPlatformText.LogOffCommand;
        ShutdownAllMenuItem.Header = CrossPlatformText.ShutdownCommand;
        RestartAllMenuItem.Header = CrossPlatformText.RestartCommand;
        LogOffAllMenuItem.Header = CrossPlatformText.LogOffCommand;
        FrequentProgramsMenuItem.Header = CrossPlatformText.FrequentProgramsMenu;
        RefreshFrequentProgramsMenuItem.Header = CrossPlatformText.RefreshFrequentPrograms;
        ManageFrequentProgramsMenuItem.Header = CrossPlatformText.ManageFrequentPrograms;
        ClearSelectedFolderSelectedMenuItem.Header = CrossPlatformText.ClearDestinationFolderOnSelectedStudents;
        ClearSelectedFolderAllMenuItem.Header = CrossPlatformText.ClearDestinationFolderOnAllOnlineStudents;
        StudentWorkMenuItem.Header = CrossPlatformText.StudentWorkMenu;
        CreateStudentWorkFolderAllMenuItem.Header = CrossPlatformText.CreateStudentWorkFolderOnAllAgents;
        CollectStudentWorkToTeacherPcMenuItem.Header = CrossPlatformText.CollectStudentWorkToTeacherPc;
        ClearStudentWorkFolderAllMenuItem.Header = CrossPlatformText.ClearStudentWorkFolderOnAllAgents;
        HelpMenuItem.Header = CrossPlatformText.Help;
        ProgramUpdatesMenuItem.Header = CrossPlatformText.ProgramUpdatesMenu;
        CheckAgentUpdateMenuItem.Header = CrossPlatformText.CheckForAgentUpdate;
        StartAgentUpdateMenuItem.Header = CrossPlatformText.StartAgentUpdate;
        UpdateSelectedMenuItem.Header = CrossPlatformText.UpdateSelectedStudents;
        UpdateAllMenuItem.Header = CrossPlatformText.UpdateAllOnlineStudents;
        CheckClientUpdateMenuItem.Header = CrossPlatformText.CheckForClientUpdate;
        AboutMenuItem.Header = CrossPlatformText.About;
        SettingsButton.Content = CrossPlatformText.Settings;
        AgentsTabItem.Header = CrossPlatformText.Agents;
        ProcessesTabItem.Header = CrossPlatformText.Processes;
        FilesTabItem.Header = CrossPlatformText.Files;
        RegistryTabItem.Header = CrossPlatformText.RegistryTab;
        RemoteManagementTabItem.Header = CrossPlatformText.RemoteManagementTab;
        RemoteManagementHintTextBlock.Text = _remoteManagementTiles.Count == 0
            ? CrossPlatformText.RemoteManagementNoScreens
            : CrossPlatformText.RemoteManagementHint;
        RefreshRemoteManagementButton.Content = CrossPlatformText.RefreshRemoteManagement;
        StartVncViewOnlyButton.Content = CrossPlatformText.StartVncViewOnly;
        StopVncButton.Content = CrossPlatformText.StopVnc;
        OpenRemoteManagementViewerButton.Content = CrossPlatformText.OpenFullscreenViewer;
        ApplyTabButtonContent(RefreshAgentsButton, CrossPlatformText.RefreshAgents, "Toolbar/agents/pc-refresh-list.png", ToolbarGlyphKind.Refresh);
        ApplyTabButtonContent(ConnectSelectedAgentButton, CrossPlatformText.ConnectSelectedAgent, "Toolbar/agents/connect.png", ToolbarGlyphKind.Link);
        ApplyTabButtonContent(AddManualAgentButton, CrossPlatformText.AddManualAgent, "Toolbar/agents/add-manual.png", ToolbarGlyphKind.Add);
        ApplyTabButtonContent(EditManualAgentButton, CrossPlatformText.EditManualAgent, "Toolbar/agents/edit-manual.png", ToolbarGlyphKind.Edit);
        ApplyTabButtonContent(RemoveManualAgentButton, CrossPlatformText.RemoveManualAgent, "Toolbar/agents/delete-manual.png", ToolbarGlyphKind.Remove);
        AgentSearchTextBox.Watermark = CrossPlatformText.SearchAgents;
        GroupFilterComboBox.ItemsSource = _allAgents.Count == 0
            ? new[] { CrossPlatformText.AllGroups }
            : _allAgents.Select(x => x.GroupName)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .Cast<object>()
                .Prepend(CrossPlatformText.AllGroups)
                .ToList();
        GroupFilterComboBox.SelectedItem = CrossPlatformText.AllGroups;
        StatusFilterComboBox.ItemsSource = new[] { CrossPlatformText.All, CrossPlatformText.Online, CrossPlatformText.Offline, CrossPlatformText.Unknown };
        StatusFilterComboBox.SelectedItem = new[] { CrossPlatformText.All, CrossPlatformText.Online, CrossPlatformText.Offline, CrossPlatformText.Unknown }
            .FirstOrDefault(x => string.Equals(x, selectedStatus, StringComparison.OrdinalIgnoreCase)) ?? CrossPlatformText.All;
        AutoReconnectCheckBox.Content = CrossPlatformText.AutoReconnect;
        ApplyTabButtonContent(RefreshProcessesButton, CrossPlatformText.Refresh, "Toolbar/processes/refresh.png", ToolbarGlyphKind.Refresh);
        ApplyTabButtonContent(KillProcessButton, CrossPlatformText.TerminateSelected, "Toolbar/processes/stop.png", ToolbarGlyphKind.Stop);
        ApplyTabButtonContent(RefreshFilesButton, CrossPlatformText.RefreshBoth, "Toolbar/files/refresh-both.png", ToolbarGlyphKind.Refresh);
        ApplyTabButtonContent(UploadButton, CrossPlatformText.UploadArrow, "Toolbar/files/upload.png", ToolbarGlyphKind.Upload);
        ApplyTabButtonContent(SendToSelectedStudentsButton, CrossPlatformText.SendToSelectedStudents, "Toolbar/files/upload-group.png", ToolbarGlyphKind.UploadGroup);
        ApplyTabButtonContent(SendToAllOnlineStudentsButton, CrossPlatformText.SendToAllOnlineStudents, "Toolbar/files/broadcast.png", ToolbarGlyphKind.Broadcast);
        ApplyTabButtonContent(DownloadButton, CrossPlatformText.DownloadArrow, "Toolbar/files/download.png", ToolbarGlyphKind.Download);
        ApplyTabButtonContent(OpenLocalButton, CrossPlatformText.OpenLocal, "Toolbar/files/open-local.png", ToolbarGlyphKind.OpenRemote);
        ApplyTabButtonContent(OpenRemoteButton, CrossPlatformText.OpenRemote, "Toolbar/files/open-remote.png", ToolbarGlyphKind.OpenRemote);
        ApplyTabButtonContent(RenameLocalButton, CrossPlatformText.RenameLocal, "Toolbar/files/rename-local.png", ToolbarGlyphKind.Edit);
        ApplyTabButtonContent(RenameRemoteButton, CrossPlatformText.RenameRemote, "Toolbar/files/rename-remote.png", ToolbarGlyphKind.Edit);
        ApplyTabButtonContent(DeleteLocalButton, CrossPlatformText.DeleteLocal, "Toolbar/files/delete-local.png", ToolbarGlyphKind.Remove);
        ApplyTabButtonContent(DeleteRemoteButton, CrossPlatformText.DeleteRemote, "Toolbar/files/delete-remote.png", ToolbarGlyphKind.Remove);
        ApplyTabButtonContent(NewRemoteFolderButton, CrossPlatformText.NewRemoteFolder, "Toolbar/files/new-folder.png", ToolbarGlyphKind.NewFolder);
        TeacherPcTextBlock.Text = CrossPlatformText.TeacherPc;
        StudentPcTextBlock.Text = CrossPlatformText.StudentPc;
        UpLocalButton.Content = CrossPlatformText.UpWithArrow;
        UpRemoteButton.Content = CrossPlatformText.UpWithArrow;
        if (string.IsNullOrWhiteSpace(LocalDriveSpaceTextBlock.Text) || LocalDriveSpaceTextBlock.Text == "Free: unknown")
        {
            LocalDriveSpaceTextBlock.Text = CrossPlatformText.DriveFreeSpaceUnknown;
        }

        if (string.IsNullOrWhiteSpace(RemoteDriveSpaceTextBlock.Text) || RemoteDriveSpaceTextBlock.Text == "Free: unknown")
        {
            RemoteDriveSpaceTextBlock.Text = CrossPlatformText.DriveFreeSpaceUnknown;
        }
        ApplyTabButtonContent(RefreshRegistryButton, CrossPlatformText.Refresh, "Toolbar/registry/refresh.png", ToolbarGlyphKind.Refresh);
        ApplyTabButtonContent(NewValueButton, CrossPlatformText.NewValue, "Toolbar/registry/new-value.png", ToolbarGlyphKind.Add);
        ApplyTabButtonContent(NewKeyButton, CrossPlatformText.NewKey, "Toolbar/registry/new-key.png", ToolbarGlyphKind.Add);
        ApplyTabButtonContent(EditValueButton, CrossPlatformText.EditValue, "Toolbar/registry/edit-value.png", ToolbarGlyphKind.Edit);
        ApplyTabButtonContent(DeleteValueButton, CrossPlatformText.DeleteValue, "Toolbar/registry/delete-value.png", ToolbarGlyphKind.Remove);
        ApplyTabButtonContent(DeleteKeyButton, CrossPlatformText.DeleteKey, "Toolbar/registry/delete-key.png", ToolbarGlyphKind.Remove);
        ApplyTabButtonContent(ExportRegistryButton, CrossPlatformText.ExportRegFile, "Toolbar/registry/export-reg.png", ToolbarGlyphKind.Download);
        ApplyTabButtonContent(ImportRegistryButton, CrossPlatformText.ImportRegFile, "Toolbar/registry/import-reg.png", ToolbarGlyphKind.Upload);
        FooterTextBlock.Text = CrossPlatformText.FooterDescription;
        if (AgentsGrid.Columns.Count >= 15)
        {
            AgentsGrid.Columns[0].Header = CrossPlatformText.BrowserLock;
            AgentsGrid.Columns[1].Header = CrossPlatformText.InputLock;
            AgentsGrid.Columns[2].Header = CrossPlatformText.Source;
            AgentsGrid.Columns[3].Header = CrossPlatformText.Status;
            AgentsGrid.Columns[4].Header = CrossPlatformText.Group;
            AgentsGrid.Columns[5].Header = CrossPlatformText.Machine;
            AgentsGrid.Columns[6].Header = CrossPlatformText.User;
            AgentsGrid.Columns[7].Header = "IP";
            AgentsGrid.Columns[8].Header = CrossPlatformText.Port;
            AgentsGrid.Columns[9].Header = "MAC";
            AgentsGrid.Columns[10].Header = CrossPlatformText.Notes;
            AgentsGrid.Columns[11].Header = CrossPlatformText.UpdateStatus;
            AgentsGrid.Columns[12].Header = CrossPlatformText.UpdateStatusDetailColumn;
            AgentsGrid.Columns[13].Header = CrossPlatformText.Version;
            AgentsGrid.Columns[14].Header = CrossPlatformText.LastSeenUtc;
        }

        if (ProcessesGrid.Columns.Count >= 6)
        {
            ProcessesGrid.Columns[0].Header = "PID";
            ProcessesGrid.Columns[1].Header = CrossPlatformText.IsUk ? "Процес" : "Process";
            ProcessesGrid.Columns[2].Header = CrossPlatformText.IsUk ? "Вікно" : "Window";
            ProcessesGrid.Columns[3].Header = "Memory";
            ProcessesGrid.Columns[4].Header = CrossPlatformText.IsUk ? "Видимий" : "Visible";
            ProcessesGrid.Columns[5].Header = CrossPlatformText.IsUk ? "Запущено UTC" : "Started UTC";
        }

        if (LocalFilesGrid.Columns.Count >= 5)
        {
            LocalFilesGrid.Columns[0].Header = CrossPlatformText.Name;
            LocalFilesGrid.Columns[1].Header = CrossPlatformText.Extension;
            LocalFilesGrid.Columns[2].Header = CrossPlatformText.Attributes;
            LocalFilesGrid.Columns[3].Header = CrossPlatformText.Size;
            LocalFilesGrid.Columns[4].Header = CrossPlatformText.ModifiedUtc;
        }

        if (RemoteFilesGrid.Columns.Count >= 5)
        {
            RemoteFilesGrid.Columns[0].Header = CrossPlatformText.Name;
            RemoteFilesGrid.Columns[1].Header = CrossPlatformText.Extension;
            RemoteFilesGrid.Columns[2].Header = CrossPlatformText.Attributes;
            RemoteFilesGrid.Columns[3].Header = CrossPlatformText.Size;
            RemoteFilesGrid.Columns[4].Header = CrossPlatformText.ModifiedUtc;
        }

        if (RegistryValuesGrid.Columns.Count >= 3)
        {
            RegistryValuesGrid.Columns[0].Header = CrossPlatformText.Name;
            RegistryValuesGrid.Columns[1].Header = CrossPlatformText.RegistryValueType;
            RegistryValuesGrid.Columns[2].Header = CrossPlatformText.RegistryValueData;
        }

        RefreshRemoteManagementButton.Content = CrossPlatformText.RefreshRemoteManagement;
        StartVncViewOnlyButton.Content = CrossPlatformText.StartVncViewOnly;
        StopVncButton.Content = CrossPlatformText.StopVnc;
        OpenRemoteManagementViewerButton.Content = CrossPlatformText.OpenFullscreenViewer;
        RemoteManagementHintTextBlock.Text = _remoteManagementTiles.Count == 0
            ? CrossPlatformText.RemoteManagementNoScreens
            : CrossPlatformText.RemoteManagementHint;

        if (StatusTextBlock.Text == "Ready. Use the Agents tab to select a student machine, then connect." ||
            StatusTextBlock.Text == "Готово. Виберіть машину на вкладці агентів і підключіться.")
        {
            StatusTextBlock.Text = CrossPlatformText.StatusReady;
        }
    }

    private static void ApplyTabButtonContent(Button button, string text, string assetPath, ToolbarGlyphKind glyphKind)
    {
        button.Content = BuildTabButtonContent(text, assetPath, glyphKind);
    }

    private static Control BuildTabButtonContent(string text, string assetPath, ToolbarGlyphKind glyphKind)
    {
        var contentGrid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,8,*"),
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };

        Control icon = CreateToolbarIcon(assetPath, glyphKind);
        Grid.SetColumn(icon, 0);

        var textBlock = new TextBlock
        {
            Text = text,
            FontSize = 10,
            Foreground = Brushes.White,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            MaxWidth = 116
        };
        Grid.SetColumn(textBlock, 2);

        contentGrid.Children.Add(icon);
        contentGrid.Children.Add(textBlock);
        return contentGrid;
    }

    private static Control CreateToolbarIcon(string assetPath, ToolbarGlyphKind glyphKind)
    {
        var bitmap = BrandingAssetLoader.LoadBitmap(assetPath);
        if (bitmap is not null)
        {
            return new Image
            {
                Width = 16,
                Height = 16,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Source = bitmap
            };
        }

        return new PathIcon
        {
            Width = 16,
            Height = 16,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Foreground = GetGlyphBrush(glyphKind),
            Data = Geometry.Parse(GetGlyphPath(glyphKind))
        };
    }

    private static IBrush GetGlyphBrush(ToolbarGlyphKind glyphKind)
        => new SolidColorBrush(glyphKind switch
        {
            ToolbarGlyphKind.Settings => Color.Parse("#4F46E5"),
            ToolbarGlyphKind.Refresh => Color.Parse("#0891B2"),
            ToolbarGlyphKind.Link => Color.Parse("#16A34A"),
            ToolbarGlyphKind.Add => Color.Parse("#22C55E"),
            ToolbarGlyphKind.Edit => Color.Parse("#F59E0B"),
            ToolbarGlyphKind.Remove => Color.Parse("#DC2626"),
            ToolbarGlyphKind.Stop => Color.Parse("#BE185D"),
            ToolbarGlyphKind.Upload => Color.Parse("#0284C7"),
            ToolbarGlyphKind.UploadGroup => Color.Parse("#2563EB"),
            ToolbarGlyphKind.Download => Color.Parse("#0E7490"),
            ToolbarGlyphKind.OpenRemote => Color.Parse("#2563EB"),
            ToolbarGlyphKind.Broadcast => Color.Parse("#7C3AED"),
            ToolbarGlyphKind.NewFolder => Color.Parse("#CA8A04"),
            _ => Color.Parse("#0F172A")
        });

    private static string GetGlyphPath(ToolbarGlyphKind glyphKind)
        => glyphKind switch
        {
            ToolbarGlyphKind.Settings => "M19.14,12.94C19.18,12.64 19.2,12.32 19.2,12C19.2,11.68 19.18,11.36 19.14,11.06L21.19,9.47C21.37,9.33 21.42,9.07 21.3,8.86L19.3,5.4C19.18,5.18 18.92,5.1 18.69,5.18L16.27,6.15C15.77,5.76 15.23,5.43 14.63,5.18L14.27,2.6C14.24,2.36 14.03,2.18 13.79,2.18H10.21C9.97,2.18 9.76,2.36 9.73,2.6L9.37,5.18C8.77,5.43 8.23,5.76 7.73,6.15L5.31,5.18C5.08,5.1 4.82,5.18 4.7,5.4L2.7,8.86C2.58,9.07 2.63,9.33 2.81,9.47L4.86,11.06C4.82,11.36 4.8,11.69 4.8,12C4.8,12.31 4.82,12.64 4.86,12.94L2.81,14.53C2.63,14.67 2.58,14.93 2.7,15.14L4.7,18.6C4.82,18.82 5.08,18.9 5.31,18.82L7.73,17.85C8.23,18.24 8.77,18.57 9.37,18.82L9.73,21.4C9.76,21.64 9.97,21.82 10.21,21.82H13.79C14.03,21.82 14.24,21.64 14.27,21.4L14.63,18.82C15.23,18.57 15.77,18.24 16.27,17.85L18.69,18.82C18.92,18.9 19.18,18.82 19.3,18.6L21.3,15.14C21.42,14.93 21.37,14.67 21.19,14.53L19.14,12.94ZM12,15.6C10.01,15.6 8.4,13.99 8.4,12C8.4,10.01 10.01,8.4 12,8.4C13.99,8.4 15.6,10.01 15.6,12C15.6,13.99 13.99,15.6 12,15.6Z",
            ToolbarGlyphKind.Refresh => "M12,4V1L8,5L12,9V6C15.31,6 18,8.69 18,12C18,15.31 15.31,18 12,18C9.16,18 6.78,16.03 6.14,13.38H4.08C4.77,17.14 8.06,20 12,20C16.42,20 20,16.42 20,12C20,7.58 16.42,4 12,4Z",
            ToolbarGlyphKind.Link => "M10.59,13.41L9.17,12L13.41,7.76L14.83,9.17M17.66,6.34C16.88,5.56 15.61,5.56 14.83,6.34L13.41,7.76L16.24,10.59L17.66,9.17C18.44,8.39 18.44,7.12 17.66,6.34M6.34,17.66C7.12,18.44 8.39,18.44 9.17,17.66L10.59,16.24L7.76,13.41L6.34,14.83C5.56,15.61 5.56,16.88 6.34,17.66M14.83,10.59L13.41,12L12,10.59L10.59,12L12,13.41L10.59,14.83C9.81,15.61 8.54,15.61 7.76,14.83C6.98,14.05 6.98,12.78 7.76,12L9.17,10.59L7.76,9.17L6.34,10.59C4.78,12.15 4.78,14.68 6.34,16.24C7.9,17.8 10.43,17.8 11.99,16.24L13.41,14.83L14.83,16.24L16.24,14.83L14.83,13.41L16.24,12C17.8,10.44 17.8,7.91 16.24,6.34C14.68,4.78 12.15,4.78 10.59,6.34L9.17,7.76L10.59,9.17L12,7.76C12.78,6.98 14.05,6.98 14.83,7.76C15.61,8.54 15.61,9.81 14.83,10.59Z",
            ToolbarGlyphKind.Add => "M19,13H13V19H11V13H5V11H11V5H13V11H19V13Z",
            ToolbarGlyphKind.Edit => "M14.06,9.02L14.98,9.94L5.92,19H5V18.08M17.66,3C17.41,3 17.16,3.1 16.97,3.29L15.13,5.13L18.87,8.87L20.71,7.03C21.1,6.64 21.1,6 20.71,5.61L18.39,3.29C18.2,3.1 17.95,3 17.66,3Z",
            ToolbarGlyphKind.Remove => "M19,13H5V11H19V13Z",
            ToolbarGlyphKind.Stop => "M6,6H18V18H6V6Z",
            ToolbarGlyphKind.Upload => "M5,20H19V18H5M19,9H15V3H9V9H5L12,16L19,9Z",
            ToolbarGlyphKind.UploadGroup => "M16,16V14C16,12.9 13.33,12 11,12S6,12.9 6,14V16H16M11,11A3,3 0 0,0 14,8A3,3 0 0,0 11,5A3,3 0 0,0 8,8A3,3 0 0,0 11,11M18,11V8H21V6H18V3H16V6H13V8H16V11H18Z",
            ToolbarGlyphKind.Download => "M5,20H19V18H5M9,4V10H5L12,17L19,10H15V4H9Z",
            ToolbarGlyphKind.OpenRemote => "M14,3V5H17.59L7.76,14.83L9.17,16.24L19,6.41V10H21V3M5,5H12V7H5V19H17V12H19V19C19,20.1 18.1,21 17,21H5C3.9,21 3,20.1 3,19V7C3,5.9 3.9,5 5,5Z",
            ToolbarGlyphKind.Broadcast => "M3,10V14H7L12,19V5L7,10H3M16.5,12C16.5,10.23 15.73,8.63 14.5,7.5L13.08,8.92C13.95,9.69 14.5,10.79 14.5,12C14.5,13.21 13.95,14.31 13.08,15.08L14.5,16.5C15.73,15.37 16.5,13.77 16.5,12M14.5,3.97L13.09,5.38C15.47,7 17,9.83 17,13C17,16.17 15.47,19 13.09,20.62L14.5,22.03C17.3,20.04 19,16.73 19,13C19,9.27 17.3,5.96 14.5,3.97Z",
            ToolbarGlyphKind.NewFolder => "M10,4L12,6H20C21.1,6 22,6.9 22,8V10H20V8H4V18H11V20H4C2.9,20 2,19.1 2,18V6C2,4.9 2.9,4 4,4H10M19,12V15H22V17H19V20H17V17H14V15H17V12H19Z",
            _ => "M12,2A10,10 0 0,1 22,12A10,10 0 0,1 12,22A10,10 0 0,1 2,12A10,10 0 0,1 12,2Z"
        };

    private void OpenLocalEntry(FileSystemEntryDto entry)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = entry.FullPath,
                UseShellExecute = true
            });
            SetStatus(CrossPlatformText.OpenedLocal(entry.Name));
        }
        catch (Exception ex)
        {
            SetStatus($"{CrossPlatformText.OpenLocalError}: {ex.Message}");
        }
    }

    private static void RenameLocalEntry(FileSystemEntryDto entry, string newName)
    {
        var safeName = ValidateLocalEntryName(newName);
        var parentDirectory = Path.GetDirectoryName(entry.FullPath);
        if (string.IsNullOrWhiteSpace(parentDirectory))
        {
            throw new InvalidOperationException("Cannot rename a root path.");
        }

        var destinationPath = Path.Combine(parentDirectory, safeName);
        if (string.Equals(entry.FullPath, destinationPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (Directory.Exists(destinationPath) || File.Exists(destinationPath))
        {
            throw new IOException($"An entry with the name '{safeName}' already exists.");
        }

        if (entry.IsDirectory)
        {
            Directory.Move(entry.FullPath, destinationPath);
            return;
        }

        File.Move(entry.FullPath, destinationPath);
    }

    private static string ValidateLocalEntryName(string name)
    {
        var trimmed = name.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            throw new InvalidOperationException("Entry name is required.");
        }

        if (!string.Equals(trimmed, Path.GetFileName(trimmed), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Only a file or folder name is allowed.");
        }

        if (trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new InvalidOperationException("The file or folder name contains invalid characters.");
        }

        return trimmed;
    }

    private enum ToolbarGlyphKind
    {
        Settings,
        Refresh,
        Link,
        Add,
        Edit,
        Remove,
        Stop,
        Upload,
        UploadGroup,
        Download,
        OpenRemote,
        Broadcast,
        NewFolder
    }
}

public sealed class RegistryNode
{
    public string Name { get; }
    public string Path { get; }
    public bool IsLoaded { get; set; }
    public ObservableCollection<RegistryNode> Children { get; } = [];

    public RegistryNode(string name, string path, bool hasChildren)
    {
        Name = name;
        Path = path;
        if (hasChildren)
            Children.Add(new RegistryNode("...", path, hasChildren: false));
    }
}
