using System.Collections.ObjectModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Teacher.Common;
using Teacher.Common.Localization;
using Teacher.Common.Contracts;
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
    private readonly ObservableCollection<DiscoveredAgentRow> _agents = [];
    private readonly ObservableCollection<ProcessInfoDto> _processes = [];
    private readonly ObservableCollection<FileSystemEntryDto> _localEntries = [];
    private readonly ObservableCollection<FileSystemEntryDto> _remoteEntries = [];
    private readonly DispatcherTimer _agentRefreshTimer = new();
    private readonly DispatcherTimer _connectionMonitorTimer = new();
    private readonly HashSet<string> _preparedStudentWorkFolders = new(StringComparer.OrdinalIgnoreCase);
    private ClientSettings _clientSettings = ClientSettings.Default;
    private List<ManualAgentEntry> _manualAgents = [];
    private List<DiscoveredAgentRow> _allAgents = [];
    private string? _remoteParentPath;
    private string? _lastConnectedAgentId;
    private string? _lastConnectedServerUrl;

    public MainWindow()
    {
        _clientSettings = _clientSettingsStore.Load();
        CrossPlatformText.SetLanguage(_clientSettings.Language);
        InitializeComponent();
        ProcessesGrid.ItemsSource = _processes;
        LocalFilesGrid.ItemsSource = _localEntries;
        RemoteFilesGrid.ItemsSource = _remoteEntries;
        AgentsGrid.ItemsSource = _agents;
        LocalPathTextBox.Text = GetDefaultLocalPath();
        _manualAgents = _manualAgentStore.Load().ToList();

        GroupFilterComboBox.ItemsSource = new[] { CrossPlatformText.AllGroups };
        GroupFilterComboBox.SelectedIndex = 0;
        StatusFilterComboBox.ItemsSource = new[] { CrossPlatformText.All, CrossPlatformText.Online, CrossPlatformText.Offline, CrossPlatformText.Unknown };
        StatusFilterComboBox.SelectedIndex = 0;
        ApplyLocalization();

        _agentRefreshTimer.Interval = TimeSpan.FromSeconds(15);
        _agentRefreshTimer.Tick += async (_, _) => await LoadAgentsAsync();
        _connectionMonitorTimer.Interval = TimeSpan.FromSeconds(10);
        _connectionMonitorTimer.Tick += async (_, _) => await MonitorConnectionAsync();

        Opened += async (_, _) =>
        {
            await LoadAgentsAsync();
            _agentRefreshTimer.Start();
            _connectionMonitorTimer.Start();
        };
        Closing += (_, _) =>
        {
            _agentRefreshTimer.Stop();
            _connectionMonitorTimer.Stop();
        };
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
    }

    private async void RefreshAgentsButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await LoadAgentsAsync();
    }

    private async void ConnectSelectedAgentButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await ConnectSelectedAgentAsync();
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
            await EnsureStudentWorkFolderOnAvailableAgentsAsync(reportSummary: false);

            SetStatus(_allAgents.Count == 0
                ? CrossPlatformText.NoAgentsAvailable
                : CrossPlatformText.MachineSummary(_allAgents.Count, discoveredAgents.Count, _manualAgents.Count));
        }, CrossPlatformText.DiscoveryError);
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
        SetStatus(CrossPlatformText.ConnectedToAgent(sourceLabel, info.MachineName, info.CurrentUser));
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
                        updatedAgents.Add(agent with
                        {
                            Status = CrossPlatformText.Online,
                            CurrentUser = info.CurrentUser,
                            BrowserLockEnabled = info.IsBrowserLockEnabled
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
                    updatedAgents.Add(agent with
                    {
                        Status = CrossPlatformText.Online,
                        CurrentUser = info?.CurrentUser ?? agent.CurrentUser,
                        BrowserLockEnabled = info?.IsBrowserLockEnabled ?? agent.BrowserLockEnabled
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
            ReplaceItems(_localEntries, entries);
            return Task.CompletedTask;
        }, CrossPlatformText.LocalBrowseError);
    }

    private async Task LoadRemoteDirectoryAsync(string? path)
    {
        await RunBusyAsync(async () =>
        {
            var client = CreateClient();
            var listing = await client.GetRemoteDirectoryAsync(path);
            if (listing is null)
            {
                SetStatus(CrossPlatformText.RemoteListingFailed);
                return;
            }

            RemotePathTextBox.Text = listing.CurrentPath;
            _remoteParentPath = listing.ParentPath;
            ReplaceItems(_remoteEntries, listing.Entries);
        }, CrossPlatformText.RemoteBrowseError);
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
            await client.UploadFileAsync(entry.FullPath, RemotePathTextBox.Text ?? string.Empty);
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
            await client.DownloadRemoteFileAsync(entry.FullPath, LocalPathTextBox.Text ?? GetDefaultLocalPath());
            await LoadLocalDirectoryAsync(LocalPathTextBox.Text);
            SetStatus(CrossPlatformText.Downloaded(entry.Name));
        }, CrossPlatformText.DownloadError);
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

    private async void AgentsGrid_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        await ConnectSelectedAgentAsync();
    }

    private async void LocalFilesGrid_OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (LocalFilesGrid.SelectedItem is FileSystemEntryDto entry && entry.IsDirectory)
        {
            await LoadLocalDirectoryAsync(entry.FullPath);
        }
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
                    await DownloadRemoteDirectoryContentsAsync(client, studentWorkPath, localWorkFolder);
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
            await client.UploadFileAsync(file.LocalPath, file.RemoteDirectory, cancellationToken);
        }
    }

    private static async Task DownloadRemoteDirectoryContentsAsync(TeacherApiClient client, string remoteDirectoryPath, string localDestinationDirectory, CancellationToken cancellationToken = default)
    {
        var listing = await client.GetRemoteDirectoryAsync(remoteDirectoryPath, cancellationToken)
                      ?? throw new InvalidOperationException("Remote listing failed.");

        Directory.CreateDirectory(localDestinationDirectory);
        foreach (var entry in listing.Entries)
        {
            if (entry.IsDirectory)
            {
                var childDirectory = Path.Combine(localDestinationDirectory, entry.Name);
                await DownloadRemoteDirectoryContentsAsync(client, entry.FullPath, childDirectory, cancellationToken);
            }
            else
            {
                await client.DownloadRemoteFileAsync(entry.FullPath, localDestinationDirectory, cancellationToken);
            }
        }
    }

    private void SaveManualAgents()
    {
        _manualAgentStore.Save(_manualAgents);
    }

    private List<DiscoveredAgentRow> GetSelectedAgents()
    {
        return AgentsGrid.SelectedItems?
            .OfType<DiscoveredAgentRow>()
            .Distinct()
            .ToList() ?? [];
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
            entry.LastWriteTimeUtc);
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
        string Version,
        DateTime LastSeenUtc,
        bool IsManual)
    {
        public bool BrowserLockEnabled { get; set; }
        public string LastSeenDisplay => LastSeenUtc == DateTime.MinValue ? string.Empty : LastSeenUtc.ToString("u");

        public static DiscoveredAgentRow FromDto(AgentDiscoveryDto dto)
        {
            return new DiscoveredAgentRow(
                dto.AgentId,
                CrossPlatformText.AutoSource,
                CrossPlatformText.Online,
                string.Empty,
                dto.MachineName,
                dto.CurrentUser,
                dto.RespondingAddress,
                dto.Port,
                string.Join(", ", dto.MacAddresses),
                string.Empty,
                dto.Version,
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
                CrossPlatformText.ManualVersion,
                DateTime.MinValue,
                true);
        }
    }

    private void ApplyLocalization()
    {
        var selectedStatus = StatusFilterComboBox.SelectedItem?.ToString();
        Title = CrossPlatformText.MainTitle;
        ConnectionMenuItem.Header = CrossPlatformText.ConnectionMenu;
        SettingsMenuItem.Header = CrossPlatformText.Settings;
        RefreshAgentsMenuItem.Header = CrossPlatformText.RefreshAgents;
        ConnectSelectedMenuItem.Header = CrossPlatformText.ConnectSelectedAgent;
        AddManualMenuItem.Header = CrossPlatformText.AddManualAgent;
        EditManualMenuItem.Header = CrossPlatformText.EditManualAgent;
        RemoveManualMenuItem.Header = CrossPlatformText.RemoveManualAgent;
        GroupCommandsMenuItem.Header = CrossPlatformText.GroupCommands;
        ClearSelectedFolderSelectedMenuItem.Header = CrossPlatformText.ClearDestinationFolderOnSelectedStudents;
        ClearSelectedFolderAllMenuItem.Header = CrossPlatformText.ClearDestinationFolderOnAllOnlineStudents;
        StudentWorkMenuItem.Header = CrossPlatformText.StudentWorkMenu;
        CreateStudentWorkFolderAllMenuItem.Header = CrossPlatformText.CreateStudentWorkFolderOnAllAgents;
        CollectStudentWorkToTeacherPcMenuItem.Header = CrossPlatformText.CollectStudentWorkToTeacherPc;
        ClearStudentWorkFolderAllMenuItem.Header = CrossPlatformText.ClearStudentWorkFolderOnAllAgents;
        HelpMenuItem.Header = CrossPlatformText.Help;
        AboutMenuItem.Header = CrossPlatformText.About;
        SettingsButton.Content = CrossPlatformText.Settings;
        AgentsTabItem.Header = CrossPlatformText.Agents;
        ProcessesTabItem.Header = CrossPlatformText.Processes;
        FilesTabItem.Header = CrossPlatformText.Files;
        RefreshAgentsButton.Content = CrossPlatformText.RefreshAgents;
        ConnectSelectedAgentButton.Content = CrossPlatformText.ConnectSelectedAgent;
        AddManualAgentButton.Content = CrossPlatformText.AddManualAgent;
        EditManualAgentButton.Content = CrossPlatformText.EditManualAgent;
        RemoveManualAgentButton.Content = CrossPlatformText.RemoveManualAgent;
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
        RefreshProcessesButton.Content = CrossPlatformText.Refresh;
        KillProcessButton.Content = CrossPlatformText.TerminateSelected;
        RefreshFilesButton.Content = CrossPlatformText.RefreshBoth;
        UploadButton.Content = CrossPlatformText.UploadArrow;
        SendToSelectedStudentsButton.Content = CrossPlatformText.SendToSelectedStudents;
        SendToAllOnlineStudentsButton.Content = CrossPlatformText.SendToAllOnlineStudents;
        DownloadButton.Content = CrossPlatformText.DownloadArrow;
        DeleteLocalButton.Content = CrossPlatformText.DeleteLocal;
        DeleteRemoteButton.Content = CrossPlatformText.DeleteRemote;
        NewRemoteFolderButton.Content = CrossPlatformText.NewRemoteFolder;
        TeacherPcTextBlock.Text = CrossPlatformText.TeacherPc;
        StudentPcTextBlock.Text = CrossPlatformText.StudentPc;
        UpLocalButton.Content = CrossPlatformText.Up;
        UpRemoteButton.Content = CrossPlatformText.Up;
        FooterTextBlock.Text = CrossPlatformText.FooterDescription;
        if (AgentsGrid.Columns.Count >= 12)
        {
            AgentsGrid.Columns[0].Header = CrossPlatformText.BrowserLock;
            AgentsGrid.Columns[1].Header = CrossPlatformText.Source;
            AgentsGrid.Columns[2].Header = CrossPlatformText.Status;
            AgentsGrid.Columns[3].Header = CrossPlatformText.Group;
            AgentsGrid.Columns[4].Header = CrossPlatformText.Machine;
            AgentsGrid.Columns[5].Header = CrossPlatformText.User;
            AgentsGrid.Columns[6].Header = "IP";
            AgentsGrid.Columns[7].Header = CrossPlatformText.Port;
            AgentsGrid.Columns[8].Header = "MAC";
            AgentsGrid.Columns[9].Header = CrossPlatformText.Notes;
            AgentsGrid.Columns[10].Header = CrossPlatformText.Version;
            AgentsGrid.Columns[11].Header = CrossPlatformText.LastSeenUtc;
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

        if (LocalFilesGrid.Columns.Count >= 4)
        {
            LocalFilesGrid.Columns[0].Header = CrossPlatformText.IsUk ? "Назва" : "Name";
            LocalFilesGrid.Columns[1].Header = CrossPlatformText.IsUk ? "Кат." : "Dir";
            LocalFilesGrid.Columns[2].Header = "Size";
            LocalFilesGrid.Columns[3].Header = CrossPlatformText.IsUk ? "Змінено UTC" : "Modified UTC";
        }

        if (RemoteFilesGrid.Columns.Count >= 4)
        {
            RemoteFilesGrid.Columns[0].Header = CrossPlatformText.IsUk ? "Назва" : "Name";
            RemoteFilesGrid.Columns[1].Header = CrossPlatformText.IsUk ? "Кат." : "Dir";
            RemoteFilesGrid.Columns[2].Header = "Size";
            RemoteFilesGrid.Columns[3].Header = CrossPlatformText.IsUk ? "Змінено UTC" : "Modified UTC";
        }
        if (StatusTextBlock.Text == "Ready. Use the Agents tab to select a student machine, then connect." ||
            StatusTextBlock.Text == "Готово. Виберіть машину на вкладці агентів і підключіться.")
        {
            StatusTextBlock.Text = CrossPlatformText.StatusReady;
        }
    }
}
