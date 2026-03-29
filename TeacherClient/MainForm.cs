using System.ComponentModel;
using Teacher.Common;
using Teacher.Common.Contracts;
using TeacherClient.Models;
using TeacherClient.Services;
using TeacherClient.Localization;

namespace TeacherClient;

public partial class MainForm : Form
{
    private const int BrowserLockColumnIndex = 0;
    private const int InputLockColumnIndex = 1;
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
    private readonly HashSet<string> _preparedStudentWorkFolders = new(StringComparer.OrdinalIgnoreCase);
    private bool _suppressBrowserLockEvents;
    private bool _suppressDriveSelectionEvents;
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
        agentsGrid.CurrentCellDirtyStateChanged += agentsGrid_CurrentCellDirtyStateChanged;
        agentsGrid.CellValueChanged += agentsGrid_CellValueChanged;
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
        PopulateLocalRoots();

        _agentRefreshTimer.Interval = 15000;
        _agentRefreshTimer.Tick += async (_, _) => await LoadDiscoveredAgentsAsync(showBusyCursor: false);
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

    private void agentsGrid_CurrentCellDirtyStateChanged(object? sender, EventArgs e)
    {
        if (agentsGrid.IsCurrentCellDirty)
        {
            agentsGrid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }
    }

    private async void agentsGrid_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
    {
        if (_suppressBrowserLockEvents || e.RowIndex < 0)
        {
            return;
        }

        if (agentsGrid.Rows[e.RowIndex].DataBoundItem is not DiscoveredAgentRow agent)
        {
            return;
        }

        var requestedValue = Convert.ToBoolean(agentsGrid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value ?? false);
        if (e.ColumnIndex == BrowserLockColumnIndex)
        {
            await ToggleBrowserLockAsync(agent, requestedValue);
        }
        else if (e.ColumnIndex == InputLockColumnIndex)
        {
            await ToggleInputLockAsync(agent, requestedValue);
        }
    }

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

        _preparedStudentWorkFolders.Clear();
        await EnsureStudentWorkFolderOnAvailableAgentsAsync(reportSummary: true);
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

    private async Task LoadDiscoveredAgentsAsync(bool showBusyCursor = true)
    {
        try
        {
            using CursorScope? cursorScope = showBusyCursor ? new CursorScope(this) : null;
            var discoveredAgents = await _agentDiscoveryService.DiscoverAsync();
            var discoveredRows = discoveredAgents.Select(DiscoveredAgentRow.FromDto).ToList();
            var manualRows = _manualAgents.Select(DiscoveredAgentRow.FromManualEntry).ToList();
            var merged = MergeAgents(manualRows, discoveredRows).ToList();
            _allAgents = (await UpdateAgentStatusesAsync(merged, discoveredRows)).ToList();
            RefreshGroupFilterOptions();
            ApplyAgentFilters();
            await EnsureStudentWorkFolderOnAvailableAgentsAsync(reportSummary: false);

            SetStatus(_allAgents.Count == 0
                ? TeacherClientText.NoAgentsAvailable
                : TeacherClientText.FormatAvailableAgents(_allAgents.Count, discoveredAgents.Count, _manualAgents.Count));
        }
        catch (Exception ex)
        {
            SetStatus($"{TeacherClientText.DiscoveryError}: {ex.Message}");
        }
    }

    private async void lockBrowsersOnAllOnlineStudentsMenuItem_Click(object? sender, EventArgs e)
    {
        var targetAgents = _allAgents
            .Where(x => string.Equals(x.Status, TeacherClientText.Online, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (targetAgents.Count == 0)
        {
            SetStatus(TeacherClientText.NoOnlineAgentsAvailableForGroupCommand);
            return;
        }

        await SetBrowserLockOnAgentsAsync(targetAgents, enabled: true);
    }

    private async void lockInputOnAllOnlineStudentsMenuItem_Click(object? sender, EventArgs e)
    {
        var targetAgents = _allAgents
            .Where(x => string.Equals(x.Status, TeacherClientText.Online, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (targetAgents.Count == 0)
        {
            SetStatus(TeacherClientText.NoOnlineAgentsAvailableForGroupCommand);
            return;
        }

        await SetInputLockOnAgentsAsync(targetAgents, enabled: true);
    }

    private async void unlockInputOnAllOnlineStudentsMenuItem_Click(object? sender, EventArgs e)
    {
        var targetAgents = _allAgents
            .Where(x => string.Equals(x.Status, TeacherClientText.Online, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (targetAgents.Count == 0)
        {
            SetStatus(TeacherClientText.NoOnlineAgentsAvailableForGroupCommand);
            return;
        }

        await SetInputLockOnAgentsAsync(targetAgents, enabled: false);
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
            SelectRoot(localDriveComboBox, info.FullName);
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
            await PopulateRemoteRootsAsync(client);
            var listing = await client.GetRemoteDirectoryAsync(path);
            if (listing is null)
            {
                SetStatus(TeacherClientText.RemoteListingFailed);
                return;
            }

            remotePathTextBox.Text = listing.CurrentPath;
            SelectRoot(remoteDriveComboBox, listing.CurrentPath);
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

    private void PopulateLocalRoots()
    {
        var roots = DriveInfo.GetDrives()
            .Select(x => x.RootDirectory.FullName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        _suppressDriveSelectionEvents = true;
        try
        {
            localDriveComboBox.Items.Clear();
            localDriveComboBox.Items.AddRange(roots);
            if (localDriveComboBox.Items.Count > 0 && localDriveComboBox.SelectedIndex < 0)
            {
                localDriveComboBox.SelectedIndex = 0;
            }
        }
        finally
        {
            _suppressDriveSelectionEvents = false;
        }
    }

    private async Task PopulateRemoteRootsAsync(TeacherApiClient client)
    {
        var roots = (await client.GetRootsAsync())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        _suppressDriveSelectionEvents = true;
        try
        {
            var currentItems = remoteDriveComboBox.Items.Cast<object>()
                .Select(x => x.ToString())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Cast<string>()
                .ToArray();

            if (currentItems.SequenceEqual(roots, StringComparer.OrdinalIgnoreCase))
            {
                return;
            }

            remoteDriveComboBox.Items.Clear();
            remoteDriveComboBox.Items.AddRange(roots);
            if (remoteDriveComboBox.Items.Count > 0 && remoteDriveComboBox.SelectedIndex < 0)
            {
                remoteDriveComboBox.SelectedIndex = 0;
            }
        }
        finally
        {
            _suppressDriveSelectionEvents = false;
        }
    }

    private void SelectRoot(ComboBox comboBox, string fullPath)
    {
        var root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrWhiteSpace(root))
        {
            return;
        }

        _suppressDriveSelectionEvents = true;
        try
        {
            var match = comboBox.Items.Cast<object>()
                .Select(x => x.ToString())
                .FirstOrDefault(x => string.Equals(x, root, StringComparison.OrdinalIgnoreCase));

            if (match is not null)
            {
                comboBox.SelectedItem = match;
            }
        }
        finally
        {
            _suppressDriveSelectionEvents = false;
        }
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

    private async void localDriveComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_suppressDriveSelectionEvents || localDriveComboBox.SelectedItem is not string root || string.IsNullOrWhiteSpace(root))
        {
            return;
        }

        await LoadLocalDirectoryAsync(root);
    }

    private async void remoteDriveComboBox_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_suppressDriveSelectionEvents || remoteDriveComboBox.SelectedItem is not string root || string.IsNullOrWhiteSpace(root))
        {
            return;
        }

        await LoadRemoteDirectoryAsync(root);
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

    private async void clearSelectedFolderOnSelectedStudentsMenuItem_Click(object? sender, EventArgs e)
    {
        var targetAgents = GetSelectedAgents();
        if (targetAgents.Count == 0)
        {
            SetStatus(TeacherClientText.ChooseAgentsForDistribution);
            return;
        }

        await ClearSelectedRemoteDirectoryAsync(targetAgents, allOnline: false);
    }

    private async void clearSelectedFolderOnAllOnlineStudentsMenuItem_Click(object? sender, EventArgs e)
    {
        var targetAgents = _allAgents
            .Where(x => string.Equals(x.Status, TeacherClientText.Online, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (targetAgents.Count == 0)
        {
            SetStatus(TeacherClientText.NoOnlineAgentsAvailableForGroupCommand);
            return;
        }

        await ClearSelectedRemoteDirectoryAsync(targetAgents, allOnline: true);
    }

    private async void collectStudentWorkFromSelectedAgentsMenuItem_Click(object? sender, EventArgs e)
    {
        var targetAgents = GetSelectedAgents();
        if (targetAgents.Count == 0)
        {
            SetStatus(TeacherClientText.ChooseAgentsForDistribution);
            return;
        }

        await CollectStudentWorkAsync(targetAgents);
    }

    private async void collectStudentWorkFromAllOnlineAgentsMenuItem_Click(object? sender, EventArgs e)
    {
        var targetAgents = _allAgents
            .Where(x => string.Equals(x.Status, TeacherClientText.Online, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (targetAgents.Count == 0)
        {
            SetStatus(TeacherClientText.NoOnlineAgentsAvailableForGroupCommand);
            return;
        }

        await CollectStudentWorkAsync(targetAgents);
    }

    private async void createStudentWorkFolderOnAllAgentsMenuItem_Click(object? sender, EventArgs e)
    {
        _preparedStudentWorkFolders.Clear();
        await EnsureStudentWorkFolderOnAvailableAgentsAsync(reportSummary: true);
    }

    private async void collectStudentWorkToTeacherPcMenuItem_Click(object? sender, EventArgs e)
    {
        var targetAgents = _allAgents
            .Where(x => string.Equals(x.Status, TeacherClientText.Online, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (targetAgents.Count == 0)
        {
            SetStatus(TeacherClientText.NoOnlineAgentsAvailableForGroupCommand);
            return;
        }

        await CollectStudentWorkAsync(targetAgents);
    }

    private async void clearStudentWorkFolderOnAllAgentsMenuItem_Click(object? sender, EventArgs e)
    {
        var targetAgents = _allAgents
            .Where(x => string.Equals(x.Status, TeacherClientText.Online, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (targetAgents.Count == 0)
        {
            SetStatus(TeacherClientText.NoOnlineAgentsAvailableForGroupCommand);
            return;
        }

        await ClearConfiguredStudentWorkDirectoryAsync(targetAgents);
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

        var destinationRoot = GetConfiguredDistributionDestinationPath();
        if (string.IsNullOrWhiteSpace(destinationRoot))
        {
            SetStatus(TeacherClientText.DistributionDestinationPathRequired);
            return;
        }

        SetStatus(TeacherClientText.PreparingDistributionPlan);
        var plan = LocalDistributionPlanner.Build(entry, destinationRoot);

        var failures = new List<string>();
        var succeeded = 0;

        using var cursorScope = new CursorScope(this);
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

    private async Task ClearSelectedRemoteDirectoryAsync(IReadOnlyList<DiscoveredAgentRow> targetAgents, bool allOnline)
    {
        var destinationRoot = GetConfiguredDistributionDestinationPath();
        if (string.IsNullOrWhiteSpace(destinationRoot))
        {
            SetStatus(TeacherClientText.ClearDestinationFolderNotConfigured);
            return;
        }

        if (MessageBox.Show(
                TeacherClientText.ClearDirectoryPrompt(destinationRoot, targetAgents.Count, allOnline),
                TeacherClientText.GroupCommandsMenu,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        var failures = new List<string>();
        var succeeded = 0;

        using var cursorScope = new CursorScope(this);
        for (var agentIndex = 0; agentIndex < targetAgents.Count; agentIndex++)
        {
            var agent = targetAgents[agentIndex];
            try
            {
                SetStatus(TeacherClientText.ClearingDirectoryProgress(agent.MachineName, destinationRoot, agentIndex + 1, targetAgents.Count));
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
            await LoadRemoteDirectoryAsync(remotePathTextBox.Text);
        }

        if (failures.Count == 0)
        {
            SetStatus(TeacherClientText.ClearDirectoryCompleted(destinationRoot, succeeded));
            return;
        }

        SetStatus(TeacherClientText.ClearDirectoryCompletedWithFailures(destinationRoot, succeeded, failures.Count));
        MessageBox.Show(
            string.Join(Environment.NewLine, failures),
            TeacherClientText.BulkCommandsResultTitle,
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
    }

    private async Task SetBrowserLockOnAgentsAsync(IReadOnlyList<DiscoveredAgentRow> targetAgents, bool enabled)
    {
        if (MessageBox.Show(
                TeacherClientText.BrowserLockPrompt(targetAgents.Count),
                TeacherClientText.GroupCommandsMenu,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        var failures = new List<string>();
        var succeeded = 0;

        using var cursorScope = new CursorScope(this);
        for (var agentIndex = 0; agentIndex < targetAgents.Count; agentIndex++)
        {
            var agent = targetAgents[agentIndex];
            try
            {
                SetStatus(TeacherClientText.BrowserLockProgress(agent.MachineName, agentIndex + 1, targetAgents.Count));
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

        if (failures.Count == 0)
        {
            SetStatus(TeacherClientText.BrowserLockCompleted(succeeded));
            return;
        }

        SetStatus(TeacherClientText.BrowserLockCompletedWithFailures(succeeded, failures.Count));
        MessageBox.Show(
            string.Join(Environment.NewLine, failures),
            TeacherClientText.BulkCommandsResultTitle,
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
    }

    private async Task EnsureStudentWorkFolderOnAvailableAgentsAsync(bool reportSummary, IReadOnlyList<DiscoveredAgentRow>? overrideTargets = null)
    {
        var studentWorkPath = GetConfiguredStudentWorkPath();
        if (string.IsNullOrWhiteSpace(studentWorkPath))
        {
            return;
        }

        var targetAgents = (overrideTargets ?? _allAgents
                .Where(x => string.Equals(x.Status, TeacherClientText.Online, StringComparison.OrdinalIgnoreCase))
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

        if (failures.Count == 0)
        {
            SetStatus(TeacherClientText.WorkFolderProvisioned(succeeded));
            return;
        }

        SetStatus(TeacherClientText.WorkFolderProvisionedWithFailures(succeeded, failures.Count));
        MessageBox.Show(
            string.Join(Environment.NewLine, failures),
            TeacherClientText.BulkCommandsResultTitle,
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
    }

    private async Task CollectStudentWorkAsync(IReadOnlyList<DiscoveredAgentRow> targetAgents)
    {
        var studentWorkPath = GetConfiguredStudentWorkPath();
        if (string.IsNullOrWhiteSpace(studentWorkPath))
        {
            SetStatus(TeacherClientText.StudentWorkFolderNotConfigured);
            return;
        }

        var localDestinationRoot = string.IsNullOrWhiteSpace(localPathTextBox.Text)
            ? Directory.GetCurrentDirectory()
            : localPathTextBox.Text;

        Directory.CreateDirectory(localDestinationRoot);
        SetStatus(TeacherClientText.PreparingWorkCollection);
        await EnsureStudentWorkFolderOnAvailableAgentsAsync(reportSummary: false, overrideTargets: targetAgents);

        var failures = new List<string>();
        var succeeded = 0;

        using var cursorScope = new CursorScope(this);
        for (var agentIndex = 0; agentIndex < targetAgents.Count; agentIndex++)
        {
            var agent = targetAgents[agentIndex];
            try
            {
                SetStatus(TeacherClientText.CollectingWorkProgress(agent.MachineName, studentWorkPath, agentIndex + 1, targetAgents.Count));
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

        if (failures.Count == 0)
        {
            SetStatus(TeacherClientText.WorkCollectionCompleted(succeeded, localDestinationRoot));
            return;
        }

        SetStatus(TeacherClientText.WorkCollectionCompletedWithFailures(succeeded, failures.Count, localDestinationRoot));
        MessageBox.Show(
            string.Join(Environment.NewLine, failures),
            TeacherClientText.BulkCommandsResultTitle,
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
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

    private async Task ClearConfiguredStudentWorkDirectoryAsync(IReadOnlyList<DiscoveredAgentRow> targetAgents)
    {
        var studentWorkPath = GetConfiguredStudentWorkPath();
        if (string.IsNullOrWhiteSpace(studentWorkPath))
        {
            SetStatus(TeacherClientText.StudentWorkFolderNotConfigured);
            return;
        }

        await EnsureStudentWorkFolderOnAvailableAgentsAsync(reportSummary: false, overrideTargets: targetAgents);

        if (MessageBox.Show(
                TeacherClientText.ClearDirectoryPrompt(studentWorkPath, targetAgents.Count, allOnline: true),
                TeacherClientText.GroupCommandsMenu,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        var succeeded = 0;
        var failed = 0;

        try
        {
            using var cursorScope = new CursorScope(this);
            for (var index = 0; index < targetAgents.Count; index++)
            {
                var agent = targetAgents[index];
                SetStatus(TeacherClientText.ClearingDirectoryProgress(agent.MachineName, studentWorkPath, index + 1, targetAgents.Count));

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
                    ? TeacherClientText.ClearDirectoryCompleted(_clientSettings.StudentWorkFolderName, succeeded)
                    : TeacherClientText.ClearDirectoryCompletedWithFailures(_clientSettings.StudentWorkFolderName, succeeded, failed));
        }
        catch (Exception ex)
        {
            SetStatus($"{TeacherClientText.BulkClearError}: {ex.Message}");
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
            reportStatus(TeacherClientText.DistributionProgress(
                agent.MachineName,
                file.DisplayPath,
                agentIndex,
                agentCount,
                fileIndex + 1,
                plan.Files.Count));
            await client.UploadFileAsync(file.LocalPath, file.RemoteDirectory, cancellationToken);
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

    private async Task ToggleBrowserLockAsync(DiscoveredAgentRow agent, bool enabled)
    {
        if (!string.Equals(agent.Status, TeacherClientText.Online, StringComparison.OrdinalIgnoreCase))
        {
            SetStatus(TeacherClientText.BrowserLockRequiresOnlineAgent);
            ApplyAgentFilters();
            return;
        }

        try
        {
            var client = new TeacherApiClient($"http://{agent.RespondingAddress}:{agent.Port}", _clientSettings.SharedSecret);
            await client.SetBrowserLockEnabledAsync(enabled);
            ReplaceAgentRow(agent with { BrowserLockEnabled = enabled });
            SetStatus(enabled ? TeacherClientText.BrowserLockEnabledFor(agent.MachineName) : TeacherClientText.BrowserLockDisabledFor(agent.MachineName));
        }
        catch (Exception ex)
        {
            SetStatus($"{TeacherClientText.BrowserLockToggleFailed}: {ex.Message}");
            ApplyAgentFilters();
        }
    }

    private async Task ToggleInputLockAsync(DiscoveredAgentRow agent, bool enabled)
    {
        if (!string.Equals(agent.Status, TeacherClientText.Online, StringComparison.OrdinalIgnoreCase))
        {
            SetStatus(TeacherClientText.InputLockRequiresOnlineAgent);
            ApplyAgentFilters();
            return;
        }

        try
        {
            var client = new TeacherApiClient($"http://{agent.RespondingAddress}:{agent.Port}", _clientSettings.SharedSecret);
            await client.SetInputLockEnabledAsync(enabled);
            ReplaceAgentRow(agent with { InputLockEnabled = enabled });
            SetStatus(enabled ? TeacherClientText.InputLockEnabledFor(agent.MachineName) : TeacherClientText.InputLockDisabledFor(agent.MachineName));
        }
        catch (Exception ex)
        {
            SetStatus($"{TeacherClientText.InputLockToggleFailed}: {ex.Message}");
            ApplyAgentFilters();
        }
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

    private void ReplaceAgentRow(DiscoveredAgentRow updated)
    {
        var index = _allAgents.FindIndex(x => string.Equals(x.AgentId, updated.AgentId, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
        {
            _allAgents[index] = updated;
        }

        ApplyAgentFilters();
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

        _suppressBrowserLockEvents = true;
        _agents = new BindingList<DiscoveredAgentRow>(filtered.ToList());
        agentsGrid.DataSource = _agents;
        _suppressBrowserLockEvents = false;
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
                            Status = TeacherClientText.Online,
                            CurrentUser = info.CurrentUser,
                            BrowserLockEnabled = info.IsBrowserLockEnabled,
                            InputLockEnabled = info.IsInputLockEnabled
                        });
                        continue;
                    }
                }
                catch
                {
                }

                updatedAgents.Add(agent with { Status = TeacherClientText.Online });
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
                        Status = TeacherClientText.Online,
                        CurrentUser = info?.CurrentUser ?? agent.CurrentUser,
                        BrowserLockEnabled = info?.IsBrowserLockEnabled ?? agent.BrowserLockEnabled,
                        InputLockEnabled = info?.IsInputLockEnabled ?? agent.InputLockEnabled
                    });
                    continue;
                }
                catch
                {
                }
            }

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
        public bool BrowserLockEnabled { get; set; }
        public bool InputLockEnabled { get; set; }
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

    private async Task SetInputLockOnAgentsAsync(IReadOnlyList<DiscoveredAgentRow> targetAgents, bool enabled)
    {
        if (MessageBox.Show(
                TeacherClientText.InputLockPrompt(targetAgents.Count, enabled),
                TeacherClientText.GroupCommandsMenu,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        var failures = new List<string>();
        var succeeded = 0;

        using var cursorScope = new CursorScope(this);
        for (var agentIndex = 0; agentIndex < targetAgents.Count; agentIndex++)
        {
            var agent = targetAgents[agentIndex];
            try
            {
                SetStatus(TeacherClientText.InputLockProgress(agent.MachineName, agentIndex + 1, targetAgents.Count, enabled));
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
            ? TeacherClientText.InputLockCompleted(succeeded, enabled)
            : TeacherClientText.InputLockCompletedWithFailures(succeeded, failures.Count, enabled));

        if (failures.Count > 0)
        {
            MessageBox.Show(
                string.Join(Environment.NewLine, failures),
                TeacherClientText.BulkInputLockError,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }
}
