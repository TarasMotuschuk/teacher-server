using System.ComponentModel;
using Teacher.Common;
using Teacher.Common.Contracts;
using TeacherClient.Models;
using TeacherClient.Services;
using TeacherClient.Localization;

namespace TeacherClient;

public partial class MainForm : Form
{
    private readonly AgentDiscoveryService _agentDiscoveryService = new();
    private readonly ManualAgentStore _manualAgentStore = new();
    private readonly ClientSettingsStore _clientSettingsStore = new();
    private readonly System.Windows.Forms.Timer _agentRefreshTimer = new();
    private readonly System.Windows.Forms.Timer _connectionMonitorTimer = new();
    private ClientSettings _clientSettings = ClientSettings.Default;
    private List<ManualAgentEntry> _manualAgents = [];
    private List<DiscoveredAgentRow> _allAgents = [];
    private BindingList<DiscoveredAgentRow> _agents = new();
    private BindingList<ProcessInfoDto> _processes = new();
    private BindingList<FileSystemEntryDto> _localEntries = new();
    private BindingList<FileSystemEntryDto> _remoteEntries = new();
    private string? _remoteParentPath;
    private string? _lastConnectedAgentId;
    private string? _lastConnectedServerUrl;

    public MainForm()
    {
        _clientSettings = _clientSettingsStore.Load();
        TeacherClientText.SetLanguage(_clientSettings.Language);
        InitializeComponent();
        processesGrid.AutoGenerateColumns = false;
        localFilesGrid.AutoGenerateColumns = false;
        remoteFilesGrid.AutoGenerateColumns = false;
        agentsGrid.AutoGenerateColumns = false;
        agentsGrid.DataSource = _agents;
        processesGrid.DataSource = _processes;
        localFilesGrid.DataSource = _localEntries;
        remoteFilesGrid.DataSource = _remoteEntries;
        _manualAgents = _manualAgentStore.Load().ToList();
        groupFilterComboBox.Items.Add(TeacherClientText.AllGroups);
        groupFilterComboBox.SelectedIndex = 0;
        statusFilterComboBox.Items.AddRange([TeacherClientText.AllStatuses, TeacherClientText.Online, TeacherClientText.Offline, TeacherClientText.Unknown]);
        statusFilterComboBox.SelectedIndex = 0;
        autoReconnectCheckBox.Checked = true;

        _agentRefreshTimer.Interval = 15000;
        _agentRefreshTimer.Tick += async (_, _) => await LoadDiscoveredAgentsAsync();
        _connectionMonitorTimer.Interval = 10000;
        _connectionMonitorTimer.Tick += async (_, _) => await MonitorConnectionAsync();
        Shown += async (_, _) =>
        {
            await LoadDiscoveredAgentsAsync();
            _agentRefreshTimer.Start();
            _connectionMonitorTimer.Start();
        };
        FormClosing += (_, _) =>
        {
            _agentRefreshTimer.Stop();
            _connectionMonitorTimer.Stop();
        };
    }

    private TeacherApiClient CreateClient() => new(GetCurrentServerUrlOrThrow(), _clientSettings.SharedSecret);

    private async void settingsButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new SettingsDialog(_clientSettings);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _clientSettings = dialog.ToSettings();
        _clientSettingsStore.Save(_clientSettings);
        TeacherClientText.SetLanguage(_clientSettings.Language);
        SetStatus(TeacherClientText.SettingsSaved);

        if (_allAgents.Count > 0)
        {
            await LoadDiscoveredAgentsAsync();
        }
    }

    private async void refreshProcessesButton_Click(object sender, EventArgs e) => await LoadProcessesAsync();

    private async void refreshAgentsButton_Click(object? sender, EventArgs e) => await LoadDiscoveredAgentsAsync();

    private async void connectSelectedAgentButton_Click(object? sender, EventArgs e)
    {
        await ConnectSelectedAgentAsync();
    }

    private async void addManualAgentButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new ManualAgentDialog();
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var entry = dialog.ToEntry();
        _manualAgents.Add(entry);
        SaveManualAgents();
        await LoadDiscoveredAgentsAsync();
        SetStatus(TeacherClientText.FormatAddedManualAgent(entry.DisplayName));
    }

    private async void editManualAgentButton_Click(object? sender, EventArgs e)
    {
        if (agentsGrid.CurrentRow?.DataBoundItem is not DiscoveredAgentRow agent || !agent.IsManual)
        {
            SetStatus(TeacherClientText.ChooseManualAgentFirst);
            return;
        }

        var existing = _manualAgents.FirstOrDefault(x => string.Equals(x.Id, agent.AgentId, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            SetStatus(TeacherClientText.ManualAgentNotFound);
            return;
        }

        using var dialog = new ManualAgentDialog(existing);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        var updated = dialog.ToEntry(existing.Id);
        var index = _manualAgents.FindIndex(x => string.Equals(x.Id, existing.Id, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            _manualAgents[index] = updated;
            SaveManualAgents();
            await LoadDiscoveredAgentsAsync();
            SetStatus(TeacherClientText.FormatUpdatedManualAgent(updated.DisplayName));
        }
    }

    private async void removeManualAgentButton_Click(object? sender, EventArgs e)
    {
        if (agentsGrid.CurrentRow?.DataBoundItem is not DiscoveredAgentRow agent || !agent.IsManual)
        {
            SetStatus(TeacherClientText.ChooseManualAgentFirst);
            return;
        }

        if (MessageBox.Show(
                TeacherClientText.RemoveManualAgentPrompt(agent.MachineName),
                TeacherClientText.Confirm,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        _manualAgents = _manualAgents
            .Where(x => !string.Equals(x.Id, agent.AgentId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        SaveManualAgents();
        await LoadDiscoveredAgentsAsync();
        SetStatus(TeacherClientText.FormatRemovedManualAgent(agent.MachineName));
    }

    private async void killProcessButton_Click(object sender, EventArgs e)
    {
        if (processesGrid.CurrentRow?.DataBoundItem is not ProcessInfoDto process)
        {
            SetStatus(TeacherClientText.ChooseProcessFirst);
            return;
        }

        if (MessageBox.Show(
                TeacherClientText.TerminateProcessPrompt(process.Name, process.Id),
                TeacherClientText.TerminateProcessTitle,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        try
        {
            using var cursorScope = new CursorScope(this);
            var client = CreateClient();
            await client.KillProcessAsync(process.Id);
            await LoadProcessesAsync();
            SetStatus(TeacherClientText.FormatProcessTerminated(process.Name));
        }
        catch (Exception ex)
        {
            SetStatus($"Kill error: {ex.Message}");
        }
    }

    private async Task LoadProcessesAsync()
    {
        try
        {
            using var cursorScope = new CursorScope(this);
            var client = CreateClient();
            var processes = await client.GetProcessesAsync();
            _processes = new BindingList<ProcessInfoDto>(processes.ToList());
            processesGrid.DataSource = _processes;
            SetStatus(TeacherClientText.FormatLoadedProcesses(processes.Count));
        }
        catch (Exception ex)
        {
            SetStatus($"{TeacherClientText.ProcessLoadError}: {ex.Message}");
        }
    }

    private async Task LoadDiscoveredAgentsAsync()
    {
        try
        {
            using var cursorScope = new CursorScope(this);
            var discoveredAgents = await _agentDiscoveryService.DiscoverAsync();
            var discoveredRows = discoveredAgents.Select(DiscoveredAgentRow.FromDto).ToList();
            var manualRows = _manualAgents.Select(DiscoveredAgentRow.FromManualEntry).ToList();
            var merged = MergeAgents(manualRows, discoveredRows).ToList();
            _allAgents = (await UpdateAgentStatusesAsync(merged, discoveredRows)).ToList();
            RefreshGroupFilterOptions();
            ApplyAgentFilters();

            SetStatus(_allAgents.Count == 0
                ? TeacherClientText.NoAgentsAvailable
                : TeacherClientText.FormatAvailableAgents(_allAgents.Count, discoveredAgents.Count, _manualAgents.Count));
        }
        catch (Exception ex)
        {
            SetStatus($"{TeacherClientText.DiscoveryError}: {ex.Message}");
        }
    }

    private async void refreshFilesButton_Click(object sender, EventArgs e)
    {
        await LoadLocalDirectoryAsync(localPathTextBox.Text);
        await LoadRemoteDirectoryAsync(remotePathTextBox.Text);
        SetStatus(TeacherClientText.PanelsRefreshed);
    }

    private Task LoadLocalDirectoryAsync(string? path)
    {
        try
        {
            using var cursorScope = new CursorScope(this);
            var currentPath = string.IsNullOrWhiteSpace(path)
                ? Directory.GetLogicalDrives().First()
                : path;
            var info = new DirectoryInfo(currentPath);
            var entries = info.EnumerateFileSystemInfos()
                .OrderByDescending(x => (x.Attributes & FileAttributes.Directory) == FileAttributes.Directory)
                .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .Select(MapLocalEntry)
                .ToList();

            localPathTextBox.Text = info.FullName;
            _localEntries = new BindingList<FileSystemEntryDto>(entries);
            localFilesGrid.DataSource = _localEntries;
        }
        catch (Exception ex)
        {
            SetStatus($"{TeacherClientText.LocalBrowseError}: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    private async Task LoadRemoteDirectoryAsync(string? path)
    {
        try
        {
            using var cursorScope = new CursorScope(this);
            var client = CreateClient();
            var listing = await client.GetRemoteDirectoryAsync(path);
            if (listing is null)
            {
                SetStatus(TeacherClientText.RemoteListingFailed);
                return;
            }

            remotePathTextBox.Text = listing.CurrentPath;
            _remoteParentPath = listing.ParentPath;
            _remoteEntries = new BindingList<FileSystemEntryDto>(listing.Entries.ToList());
            remoteFilesGrid.DataSource = _remoteEntries;
        }
        catch (Exception ex)
        {
            SetStatus($"{TeacherClientText.RemoteBrowseError}: {ex.Message}");
        }
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

    private async void localFilesGrid_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || localFilesGrid.Rows[e.RowIndex].DataBoundItem is not FileSystemEntryDto entry || !entry.IsDirectory)
        {
            return;
        }

        await LoadLocalDirectoryAsync(entry.FullPath);
    }

    private async void remoteFilesGrid_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || remoteFilesGrid.Rows[e.RowIndex].DataBoundItem is not FileSystemEntryDto entry || !entry.IsDirectory)
        {
            return;
        }

        await LoadRemoteDirectoryAsync(entry.FullPath);
    }

    private async void upLocalButton_Click(object sender, EventArgs e)
    {
        var parent = Directory.GetParent(localPathTextBox.Text)?.FullName;
        if (!string.IsNullOrWhiteSpace(parent))
        {
            await LoadLocalDirectoryAsync(parent);
        }
    }

    private async void upRemoteButton_Click(object sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_remoteParentPath))
        {
            await LoadRemoteDirectoryAsync(_remoteParentPath);
        }
    }

    private async void agentsGrid_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0)
        {
            return;
        }

        await ConnectSelectedAgentAsync();
    }

    private async void uploadButton_Click(object sender, EventArgs e)
    {
        if (localFilesGrid.CurrentRow?.DataBoundItem is not FileSystemEntryDto entry || entry.IsDirectory)
        {
            SetStatus(TeacherClientText.ChooseLocalFileToUpload);
            return;
        }

        try
        {
            using var cursorScope = new CursorScope(this);
            var client = CreateClient();
            await client.UploadFileAsync(entry.FullPath, remotePathTextBox.Text);
            await LoadRemoteDirectoryAsync(remotePathTextBox.Text);
            SetStatus(TeacherClientText.FormatUploaded(entry.Name));
        }
        catch (Exception ex)
        {
            SetStatus($"{TeacherClientText.UploadError}: {ex.Message}");
        }
    }

    private async void sendToSelectedStudentsButton_Click(object? sender, EventArgs e)
    {
        var targetAgents = GetSelectedAgents();
        if (targetAgents.Count == 0)
        {
            SetStatus(TeacherClientText.ChooseAgentsForDistribution);
            return;
        }

        await DistributeLocalSelectionAsync(targetAgents);
    }

    private async void sendToAllOnlineStudentsButton_Click(object? sender, EventArgs e)
    {
        var targetAgents = _allAgents
            .Where(x => string.Equals(x.Status, TeacherClientText.Online, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (targetAgents.Count == 0)
        {
            SetStatus(TeacherClientText.NoOnlineAgentsAvailableForDistribution);
            return;
        }

        await DistributeLocalSelectionAsync(targetAgents);
    }

    private async void downloadButton_Click(object sender, EventArgs e)
    {
        if (remoteFilesGrid.CurrentRow?.DataBoundItem is not FileSystemEntryDto entry || entry.IsDirectory)
        {
            SetStatus(TeacherClientText.ChooseRemoteFileToDownload);
            return;
        }

        try
        {
            using var cursorScope = new CursorScope(this);
            var client = CreateClient();
            await client.DownloadRemoteFileAsync(entry.FullPath, localPathTextBox.Text);
            await LoadLocalDirectoryAsync(localPathTextBox.Text);
            SetStatus(TeacherClientText.FormatDownloaded(entry.Name));
        }
        catch (Exception ex)
        {
            SetStatus($"{TeacherClientText.DownloadError}: {ex.Message}");
        }
    }

    private async void deleteLocalButton_Click(object sender, EventArgs e)
    {
        if (localFilesGrid.CurrentRow?.DataBoundItem is not FileSystemEntryDto entry)
        {
            SetStatus(TeacherClientText.ChooseLocalEntryFirst);
            return;
        }

        if (MessageBox.Show(
                TeacherClientText.DeleteLocalEntryPrompt(entry.Name),
                TeacherClientText.DeleteLocalEntryTitle,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        try
        {
            using var cursorScope = new CursorScope(this);
            if (entry.IsDirectory)
            {
                Directory.Delete(entry.FullPath, recursive: true);
            }
            else
            {
                File.Delete(entry.FullPath);
            }

            await LoadLocalDirectoryAsync(localPathTextBox.Text);
            SetStatus(TeacherClientText.FormatDeletedLocal(entry.Name));
        }
        catch (Exception ex)
        {
            SetStatus($"{TeacherClientText.LocalDeleteError}: {ex.Message}");
        }
    }

    private async void deleteRemoteButton_Click(object sender, EventArgs e)
    {
        if (remoteFilesGrid.CurrentRow?.DataBoundItem is not FileSystemEntryDto entry)
        {
            SetStatus(TeacherClientText.ChooseRemoteEntryFirst);
            return;
        }

        if (MessageBox.Show(
                TeacherClientText.DeleteRemoteEntryPrompt(entry.Name),
                TeacherClientText.DeleteRemoteEntryTitle,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        try
        {
            using var cursorScope = new CursorScope(this);
            var client = CreateClient();
            await client.DeleteRemoteEntryAsync(entry.FullPath);
            await LoadRemoteDirectoryAsync(remotePathTextBox.Text);
            SetStatus(TeacherClientText.FormatDeletedRemote(entry.Name));
        }
        catch (Exception ex)
        {
            SetStatus($"{TeacherClientText.RemoteDeleteError}: {ex.Message}");
        }
    }

    private async void newRemoteFolderButton_Click(object sender, EventArgs e)
    {
        using var dialog = new InputDialog(TeacherClientText.CreateRemoteFolderTitle, TeacherClientText.FolderName, TeacherClientText.NewFolderDefaultName);
        if (dialog.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(dialog.Value))
        {
            return;
        }

        try
        {
            using var cursorScope = new CursorScope(this);
            var client = CreateClient();
            await client.CreateRemoteDirectoryAsync(remotePathTextBox.Text, dialog.Value);
            await LoadRemoteDirectoryAsync(remotePathTextBox.Text);
            SetStatus(TeacherClientText.FormatCreatedRemoteFolder(dialog.Value));
        }
        catch (Exception ex)
        {
            SetStatus($"{TeacherClientText.CreateFolderError}: {ex.Message}");
        }
    }

    private void aboutMenuItem_Click(object? sender, EventArgs e)
    {
        using var dialog = new AboutDialog();
        dialog.ShowDialog(this);
    }

    private async Task ConnectSelectedAgentAsync()
    {
        if (agentsGrid.CurrentRow?.DataBoundItem is not DiscoveredAgentRow agent)
        {
            SetStatus(TeacherClientText.ChooseAgentFirst);
            return;
        }

        await ConnectToServerAsync($"http://{agent.RespondingAddress}:{agent.Port}", agent, agent.Source);
    }

    private async Task DistributeLocalSelectionAsync(IReadOnlyList<DiscoveredAgentRow> targetAgents)
    {
        if (localFilesGrid.CurrentRow?.DataBoundItem is not FileSystemEntryDto entry)
        {
            SetStatus(TeacherClientText.ChooseLocalFileOrFolderToDistribute);
            return;
        }

        var destinationRoot = RemoteWindowsPath.Normalize(_clientSettings.BulkCopyDestinationPath);
        if (string.IsNullOrWhiteSpace(destinationRoot))
        {
            SetStatus(TeacherClientText.DistributionDestinationPathRequired);
            return;
        }

        var failures = new List<string>();
        var succeeded = 0;

        using var cursorScope = new CursorScope(this);
        foreach (var agent in targetAgents)
        {
            try
            {
                var client = new TeacherApiClient($"http://{agent.RespondingAddress}:{agent.Port}", _clientSettings.SharedSecret);
                await CopyEntryToAgentAsync(client, entry, destinationRoot);
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
            await LoadRemoteDirectoryAsync(remotePathTextBox.Text);
        }

        if (failures.Count == 0)
        {
            SetStatus(TeacherClientText.DistributionCompleted(entry.Name, succeeded));
            return;
        }

        SetStatus(TeacherClientText.DistributionCompletedWithFailures(entry.Name, succeeded, failures.Count));
        MessageBox.Show(
            string.Join(Environment.NewLine, failures),
            TeacherClientText.BulkCopyResultTitle,
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
    }

    private static async Task CopyEntryToAgentAsync(TeacherApiClient client, FileSystemEntryDto entry, string destinationRoot, CancellationToken cancellationToken = default)
    {
        await client.EnsureRemoteDirectoryPathAsync(destinationRoot, cancellationToken);

        if (!entry.IsDirectory)
        {
            await client.UploadFileAsync(entry.FullPath, destinationRoot, cancellationToken);
            return;
        }

        var remoteRoot = RemoteWindowsPath.Combine(destinationRoot, entry.Name);
        await client.EnsureRemoteDirectoryPathAsync(remoteRoot, cancellationToken);

        foreach (var directory in Directory.EnumerateDirectories(entry.FullPath, "*", SearchOption.AllDirectories))
        {
            var relativeDirectory = Path.GetRelativePath(entry.FullPath, directory);
            var remoteDirectory = RemoteWindowsPath.CombineSegments(remoteRoot, relativeDirectory);
            await client.EnsureRemoteDirectoryPathAsync(remoteDirectory, cancellationToken);
        }

        foreach (var filePath in Directory.EnumerateFiles(entry.FullPath, "*", SearchOption.AllDirectories))
        {
            var relativeFileDirectory = Path.GetRelativePath(entry.FullPath, Path.GetDirectoryName(filePath) ?? entry.FullPath);
            var remoteDirectory = string.Equals(relativeFileDirectory, ".", StringComparison.Ordinal)
                ? remoteRoot
                : RemoteWindowsPath.CombineSegments(remoteRoot, relativeFileDirectory);

            await client.EnsureRemoteDirectoryPathAsync(remoteDirectory, cancellationToken);
            await client.UploadFileAsync(filePath, remoteDirectory, cancellationToken);
        }
    }

    private void SetStatus(string text) => statusLabel.Text = text;

    private async Task ConnectToServerAsync(string serverUrl, DiscoveredAgentRow? agent, string sourceLabel)
    {
        using var cursorScope = new CursorScope(this);
        var client = new TeacherApiClient(serverUrl, _clientSettings.SharedSecret);
        var info = await client.GetServerInfoAsync();
        if (info is null)
        {
            SetStatus(TeacherClientText.ConnectionFailed);
            return;
        }

        _lastConnectedAgentId = agent?.AgentId;
        _lastConnectedServerUrl = serverUrl;
        SetStatus(TeacherClientText.FormatConnectedToAgent(sourceLabel, info.MachineName, info.CurrentUser));
        await LoadProcessesAsync();
        await LoadLocalDirectoryAsync(localPathTextBox.Text);
        await LoadRemoteDirectoryAsync(remotePathTextBox.Text);
    }

    private string GetCurrentServerUrlOrThrow()
    {
        if (string.IsNullOrWhiteSpace(_lastConnectedServerUrl))
        {
            throw new InvalidOperationException(TeacherClientText.ConnectFromAgentsTabFirst);
        }

        return _lastConnectedServerUrl;
    }

    private void SaveManualAgents()
    {
        _manualAgentStore.Save(_manualAgents);
    }

    private List<DiscoveredAgentRow> GetSelectedAgents()
    {
        return agentsGrid.SelectedRows
            .Cast<DataGridViewRow>()
            .Select(x => x.DataBoundItem)
            .OfType<DiscoveredAgentRow>()
            .Distinct()
            .ToList();
    }

    private void agentFilters_Changed(object? sender, EventArgs e)
    {
        ApplyAgentFilters();
    }

    private void ApplyAgentFilters()
    {
        var search = agentSearchTextBox.Text.Trim();
        var selectedGroup = groupFilterComboBox.SelectedItem?.ToString() ?? TeacherClientText.AllGroups;
        var selectedStatus = statusFilterComboBox.SelectedItem?.ToString() ?? TeacherClientText.AllStatuses;

        var filtered = _allAgents
            .Where(agent => selectedGroup == TeacherClientText.AllGroups ||
                            string.Equals(agent.GroupName, selectedGroup, StringComparison.OrdinalIgnoreCase))
            .Where(agent => selectedStatus == TeacherClientText.AllStatuses ||
                            string.Equals(agent.Status, selectedStatus, StringComparison.OrdinalIgnoreCase))
            .Where(agent =>
                string.IsNullOrWhiteSpace(search) ||
                agent.MachineName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                agent.RespondingAddress.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                agent.CurrentUser.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                agent.Notes.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                agent.GroupName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                agent.MacAddressesDisplay.Contains(search, StringComparison.OrdinalIgnoreCase));

        _agents = new BindingList<DiscoveredAgentRow>(filtered.ToList());
        agentsGrid.DataSource = _agents;
    }

    private void RefreshGroupFilterOptions()
    {
        var groups = _allAgents
            .Select(x => x.GroupName)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var currentSelection = groupFilterComboBox.SelectedItem?.ToString() ?? TeacherClientText.AllGroups;
        groupFilterComboBox.Items.Clear();
        groupFilterComboBox.Items.Add(TeacherClientText.AllGroups);
        foreach (var group in groups)
        {
            groupFilterComboBox.Items.Add(group);
        }

        groupFilterComboBox.SelectedItem = groupFilterComboBox.Items.Contains(currentSelection)
            ? currentSelection
            : TeacherClientText.AllGroups;
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
            if (onlineEndpoints.Contains($"{agent.RespondingAddress}:{agent.Port}"))
            {
                updatedAgents.Add(agent with { Status = TeacherClientText.Online });
                continue;
            }

            var reachabilityClient = new TeacherApiClient(
                $"http://{agent.RespondingAddress}:{agent.Port}",
                _clientSettings.SharedSecret);

            var isReachable = await reachabilityClient.IsServerReachableAsync();
            updatedAgents.Add(agent with
            {
                Status = isReachable ? TeacherClientText.Online : TeacherClientText.Offline
            });
        }

        return updatedAgents;
    }

    private async Task MonitorConnectionAsync()
    {
        if (!autoReconnectCheckBox.Checked || string.IsNullOrWhiteSpace(_lastConnectedServerUrl))
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

            if (targetAgent is null || string.Equals(targetAgent.Status, TeacherClientText.Offline, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            await ConnectToServerAsync($"http://{targetAgent.RespondingAddress}:{targetAgent.Port}", targetAgent, TeacherClientText.AutoReconnect);
        }
        catch
        {
        }
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
                    Source = TeacherClientText.ManualAutoSource,
                    Status = TeacherClientText.Online,
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
        public string LastSeenDisplay => LastSeenUtc == DateTime.MinValue ? string.Empty : LastSeenUtc.ToString("u");

        public static DiscoveredAgentRow FromDto(AgentDiscoveryDto dto)
        {
            return new DiscoveredAgentRow(
                dto.AgentId,
                TeacherClientText.AutoSource,
                TeacherClientText.Online,
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
                TeacherClientText.ManualSource,
                TeacherClientText.Unknown,
                entry.GroupName,
                entry.DisplayName,
                string.Empty,
                entry.IpAddress,
                entry.Port,
                entry.MacAddress,
                entry.Notes,
                TeacherClientText.ManualVersion,
                DateTime.MinValue,
                true);
        }
    }

    private sealed class CursorScope : IDisposable
    {
        private readonly Control _control;
        private readonly Cursor _previousCursor;

        public CursorScope(Control control)
        {
            _control = control;
            _previousCursor = control.Cursor;
            control.Cursor = Cursors.WaitCursor;
        }

        public void Dispose()
        {
            _control.Cursor = _previousCursor;
        }
    }
}
