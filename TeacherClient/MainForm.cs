using System.ComponentModel;
using Teacher.Common.Contracts;
using TeacherClient.Models;
using TeacherClient.Services;

namespace TeacherClient;

public partial class MainForm : Form
{
    private readonly AgentDiscoveryService _agentDiscoveryService = new();
    private readonly ManualAgentStore _manualAgentStore = new();
    private readonly System.Windows.Forms.Timer _agentRefreshTimer = new();
    private readonly System.Windows.Forms.Timer _connectionMonitorTimer = new();
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
        InitializeComponent();
        processesGrid.AutoGenerateColumns = false;
        localFilesGrid.AutoGenerateColumns = false;
        remoteFilesGrid.AutoGenerateColumns = false;
        agentsGrid.AutoGenerateColumns = false;
        agentsGrid.DataSource = _agents;
        processesGrid.DataSource = _processes;
        localFilesGrid.DataSource = _localEntries;
        remoteFilesGrid.DataSource = _remoteEntries;
        serverUrlTextBox.Text = "http://127.0.0.1:5055";
        sharedSecretTextBox.Text = "change-this-secret";
        _manualAgents = _manualAgentStore.Load().ToList();
        groupFilterComboBox.Items.Add("All groups");
        groupFilterComboBox.SelectedIndex = 0;
        statusFilterComboBox.Items.AddRange(["All", "Online", "Offline", "Unknown"]);
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

    private TeacherApiClient CreateClient() => new(serverUrlTextBox.Text.Trim(), sharedSecretTextBox.Text.Trim());

    private async void connectButton_Click(object sender, EventArgs e)
    {
        try
        {
            await ConnectToServerAsync(serverUrlTextBox.Text.Trim(), null, "manual");
        }
        catch (Exception ex)
        {
            SetStatus($"Connect error: {ex.Message}");
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
        SetStatus($"Added manual agent {entry.DisplayName}");
    }

    private async void editManualAgentButton_Click(object? sender, EventArgs e)
    {
        if (agentsGrid.CurrentRow?.DataBoundItem is not DiscoveredAgentRow agent || !agent.IsManual)
        {
            SetStatus("Choose a manual agent first.");
            return;
        }

        var existing = _manualAgents.FirstOrDefault(x => string.Equals(x.Id, agent.AgentId, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            SetStatus("Manual agent not found.");
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
            SetStatus($"Updated manual agent {updated.DisplayName}");
        }
    }

    private async void removeManualAgentButton_Click(object? sender, EventArgs e)
    {
        if (agentsGrid.CurrentRow?.DataBoundItem is not DiscoveredAgentRow agent || !agent.IsManual)
        {
            SetStatus("Choose a manual agent first.");
            return;
        }

        if (MessageBox.Show(
                $"Remove manual agent {agent.MachineName}?",
                "Confirm",
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
        SetStatus($"Removed manual agent {agent.MachineName}");
    }

    private async void killProcessButton_Click(object sender, EventArgs e)
    {
        if (processesGrid.CurrentRow?.DataBoundItem is not ProcessInfoDto process)
        {
            return;
        }

        if (MessageBox.Show(
                $"Terminate process {process.Name} ({process.Id})?",
                "Confirm",
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
            SetStatus($"Process {process.Name} terminated");
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
            SetStatus($"Loaded {processes.Count} processes");
        }
        catch (Exception ex)
        {
            SetStatus($"Process load error: {ex.Message}");
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
                ? "No agents available."
                : $"Available agents: {_allAgents.Count} total, {discoveredAgents.Count} discovered, {_manualAgents.Count} manual");
        }
        catch (Exception ex)
        {
            SetStatus($"Discovery error: {ex.Message}");
        }
    }

    private async void refreshFilesButton_Click(object sender, EventArgs e)
    {
        await LoadLocalDirectoryAsync(localPathTextBox.Text);
        await LoadRemoteDirectoryAsync(remotePathTextBox.Text);
        SetStatus("Panels refreshed");
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
            SetStatus($"Local browse error: {ex.Message}");
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
                SetStatus("Remote listing failed.");
                return;
            }

            remotePathTextBox.Text = listing.CurrentPath;
            _remoteParentPath = listing.ParentPath;
            _remoteEntries = new BindingList<FileSystemEntryDto>(listing.Entries.ToList());
            remoteFilesGrid.DataSource = _remoteEntries;
        }
        catch (Exception ex)
        {
            SetStatus($"Remote browse error: {ex.Message}");
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
            SetStatus("Choose a local file to upload.");
            return;
        }

        try
        {
            using var cursorScope = new CursorScope(this);
            var client = CreateClient();
            await client.UploadFileAsync(entry.FullPath, remotePathTextBox.Text);
            await LoadRemoteDirectoryAsync(remotePathTextBox.Text);
            SetStatus($"Uploaded {entry.Name}");
        }
        catch (Exception ex)
        {
            SetStatus($"Upload error: {ex.Message}");
        }
    }

    private async void downloadButton_Click(object sender, EventArgs e)
    {
        if (remoteFilesGrid.CurrentRow?.DataBoundItem is not FileSystemEntryDto entry || entry.IsDirectory)
        {
            SetStatus("Choose a remote file to download.");
            return;
        }

        try
        {
            using var cursorScope = new CursorScope(this);
            var client = CreateClient();
            await client.DownloadRemoteFileAsync(entry.FullPath, localPathTextBox.Text);
            await LoadLocalDirectoryAsync(localPathTextBox.Text);
            SetStatus($"Downloaded {entry.Name}");
        }
        catch (Exception ex)
        {
            SetStatus($"Download error: {ex.Message}");
        }
    }

    private async void deleteLocalButton_Click(object sender, EventArgs e)
    {
        if (localFilesGrid.CurrentRow?.DataBoundItem is not FileSystemEntryDto entry)
        {
            return;
        }

        if (MessageBox.Show(
                $"Delete local entry {entry.Name}?",
                "Confirm",
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
            SetStatus($"Deleted local entry {entry.Name}");
        }
        catch (Exception ex)
        {
            SetStatus($"Local delete error: {ex.Message}");
        }
    }

    private async void deleteRemoteButton_Click(object sender, EventArgs e)
    {
        if (remoteFilesGrid.CurrentRow?.DataBoundItem is not FileSystemEntryDto entry)
        {
            return;
        }

        if (MessageBox.Show(
                $"Delete remote entry {entry.Name}?",
                "Confirm",
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
            SetStatus($"Deleted remote entry {entry.Name}");
        }
        catch (Exception ex)
        {
            SetStatus($"Remote delete error: {ex.Message}");
        }
    }

    private async void newRemoteFolderButton_Click(object sender, EventArgs e)
    {
        using var dialog = new InputDialog("Create remote folder", "Folder name:", "NewFolder");
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
            SetStatus($"Created remote folder {dialog.Value}");
        }
        catch (Exception ex)
        {
            SetStatus($"Create folder error: {ex.Message}");
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
            SetStatus("Choose an agent first.");
            return;
        }

        await ConnectToServerAsync($"http://{agent.RespondingAddress}:{agent.Port}", agent, agent.Source.ToLowerInvariant());
    }

    private void SetStatus(string text) => statusLabel.Text = text;

    private async Task ConnectToServerAsync(string serverUrl, DiscoveredAgentRow? agent, string sourceLabel)
    {
        using var cursorScope = new CursorScope(this);
        serverUrlTextBox.Text = serverUrl;
        var client = CreateClient();
        var info = await client.GetServerInfoAsync();
        if (info is null)
        {
            SetStatus("Connection failed.");
            return;
        }

        _lastConnectedAgentId = agent?.AgentId;
        _lastConnectedServerUrl = serverUrl;
        SetStatus($"Connected to {sourceLabel} agent {info.MachineName} ({info.CurrentUser})");
        await LoadProcessesAsync();
        await LoadLocalDirectoryAsync(localPathTextBox.Text);
        await LoadRemoteDirectoryAsync(remotePathTextBox.Text);
    }

    private void SaveManualAgents()
    {
        _manualAgentStore.Save(_manualAgents);
    }

    private void agentFilters_Changed(object? sender, EventArgs e)
    {
        ApplyAgentFilters();
    }

    private void ApplyAgentFilters()
    {
        var search = agentSearchTextBox.Text.Trim();
        var selectedGroup = groupFilterComboBox.SelectedItem?.ToString() ?? "All groups";
        var selectedStatus = statusFilterComboBox.SelectedItem?.ToString() ?? "All";

        var filtered = _allAgents
            .Where(agent => selectedGroup == "All groups" ||
                            string.Equals(agent.GroupName, selectedGroup, StringComparison.OrdinalIgnoreCase))
            .Where(agent => selectedStatus == "All" ||
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

        var currentSelection = groupFilterComboBox.SelectedItem?.ToString() ?? "All groups";
        groupFilterComboBox.Items.Clear();
        groupFilterComboBox.Items.Add("All groups");
        foreach (var group in groups)
        {
            groupFilterComboBox.Items.Add(group);
        }

        groupFilterComboBox.SelectedItem = groupFilterComboBox.Items.Contains(currentSelection)
            ? currentSelection
            : "All groups";
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
                updatedAgents.Add(agent with { Status = "Online" });
                continue;
            }

            var reachabilityClient = new TeacherApiClient(
                $"http://{agent.RespondingAddress}:{agent.Port}",
                sharedSecretTextBox.Text.Trim());

            var isReachable = await reachabilityClient.IsServerReachableAsync();
            updatedAgents.Add(agent with
            {
                Status = isReachable ? "Online" : "Offline"
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
            var currentClient = new TeacherApiClient(_lastConnectedServerUrl, sharedSecretTextBox.Text.Trim());
            if (await currentClient.IsServerReachableAsync())
            {
                return;
            }

            var targetAgent = _allAgents.FirstOrDefault(x =>
                (!string.IsNullOrWhiteSpace(_lastConnectedAgentId) &&
                 string.Equals(x.AgentId, _lastConnectedAgentId, StringComparison.OrdinalIgnoreCase)) ||
                string.Equals($"http://{x.RespondingAddress}:{x.Port}", _lastConnectedServerUrl, StringComparison.OrdinalIgnoreCase));

            if (targetAgent is null || string.Equals(targetAgent.Status, "Offline", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            await ConnectToServerAsync($"http://{targetAgent.RespondingAddress}:{targetAgent.Port}", targetAgent, "auto-reconnect");
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
                    Source = "Manual+Auto",
                    Status = "Online",
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
                "Auto",
                "Online",
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
                "Manual",
                "Unknown",
                entry.GroupName,
                entry.DisplayName,
                string.Empty,
                entry.IpAddress,
                entry.Port,
                entry.MacAddress,
                entry.Notes,
                "Manual",
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
