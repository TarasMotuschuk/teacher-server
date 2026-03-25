using System.ComponentModel;
using Teacher.Common.Contracts;
using TeacherClient.Services;

namespace TeacherClient;

public partial class MainForm : Form
{
    private readonly AgentDiscoveryService _agentDiscoveryService = new();
    private readonly System.Windows.Forms.Timer _agentRefreshTimer = new();
    private BindingList<DiscoveredAgentRow> _agents = new();
    private BindingList<ProcessInfoDto> _processes = new();
    private BindingList<FileSystemEntryDto> _localEntries = new();
    private BindingList<FileSystemEntryDto> _remoteEntries = new();
    private string? _remoteParentPath;

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

        _agentRefreshTimer.Interval = 15000;
        _agentRefreshTimer.Tick += async (_, _) => await LoadDiscoveredAgentsAsync();
        Shown += async (_, _) =>
        {
            await LoadDiscoveredAgentsAsync();
            _agentRefreshTimer.Start();
        };
        FormClosing += (_, _) => _agentRefreshTimer.Stop();
    }

    private TeacherApiClient CreateClient() => new(serverUrlTextBox.Text.Trim(), sharedSecretTextBox.Text.Trim());

    private async void connectButton_Click(object sender, EventArgs e)
    {
        try
        {
            using var cursorScope = new CursorScope(this);
            var client = CreateClient();
            var info = await client.GetServerInfoAsync();
            if (info is null)
            {
                SetStatus("Connection failed.");
                return;
            }

            SetStatus($"Connected to {info.MachineName} ({info.CurrentUser})");
            await LoadProcessesAsync();
            await LoadLocalDirectoryAsync(localPathTextBox.Text);
            await LoadRemoteDirectoryAsync(remotePathTextBox.Text);
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
            _agents = new BindingList<DiscoveredAgentRow>(discoveredAgents.Select(DiscoveredAgentRow.FromDto).ToList());
            agentsGrid.DataSource = _agents;
            SetStatus(discoveredAgents.Count == 0
                ? "No agents discovered."
                : $"Discovered {discoveredAgents.Count} agent(s)");
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
            SetStatus("Choose a discovered agent first.");
            return;
        }

        serverUrlTextBox.Text = $"http://{agent.RespondingAddress}:{agent.Port}";
        await LoadProcessesAsync();
        await LoadLocalDirectoryAsync(localPathTextBox.Text);
        await LoadRemoteDirectoryAsync(remotePathTextBox.Text);
        SetStatus($"Connected to discovered agent {agent.MachineName}");
    }

    private void SetStatus(string text) => statusLabel.Text = text;

    private sealed record DiscoveredAgentRow(
        string AgentId,
        string MachineName,
        string CurrentUser,
        string RespondingAddress,
        int Port,
        string MacAddressesDisplay,
        string Version,
        DateTime LastSeenUtc)
    {
        public static DiscoveredAgentRow FromDto(AgentDiscoveryDto dto)
        {
            return new DiscoveredAgentRow(
                dto.AgentId,
                dto.MachineName,
                dto.CurrentUser,
                dto.RespondingAddress,
                dto.Port,
                string.Join(", ", dto.MacAddresses),
                dto.Version,
                dto.LastSeenUtc);
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
