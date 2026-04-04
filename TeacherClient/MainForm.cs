using System.ComponentModel;
using System.Diagnostics;
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
    private readonly FrequentProgramStore _frequentProgramStore = new();
    private readonly TeacherUpdatePreparationService _updatePreparationService =
        new(GetUpdatePreparationRootDirectory());
    private readonly TeacherClientUpdateService _clientUpdateService =
        new(GetClientUpdateRootDirectory(), Application.ProductVersion);
    private readonly System.Windows.Forms.Timer _agentRefreshTimer = new();
    private readonly System.Windows.Forms.Timer _connectionMonitorTimer = new();
    private readonly System.Windows.Forms.Timer _updateStatusTimer = new();
    private ClientSettings _clientSettings = ClientSettings.Default;
    private List<ManualAgentEntry> _manualAgents = [];
    private List<FrequentProgramEntry> _frequentPrograms = [];
    private List<DiscoveredAgentRow> _allAgents = [];
    private BindingList<DiscoveredAgentRow> _agents = new();
    private BindingList<ProcessInfoDto> _processes = new();
    private BindingList<FileSystemEntryDto> _localEntries = new();
    private BindingList<FileSystemEntryDto> _remoteEntries = new();
    private BindingList<RegistryValueDto> _registryValues = new();
    private readonly HashSet<string> _preparedStudentWorkFolders = new(StringComparer.OrdinalIgnoreCase);
    private bool _suppressBrowserLockEvents;
    private bool _suppressDriveSelectionEvents;
    private string? _remoteParentPath;
    private string? _lastConnectedAgentId;
    private string? _lastConnectedServerUrl;
    private string? _lastConnectedMachineName;

    public MainForm()
    {
        _clientSettings = _clientSettingsStore.Load();
        TeacherClientText.SetLanguage(_clientSettings.Language);
        InitializeComponent();
        Icon = AppIconLoader.Load();
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
        registryValuesGrid.AutoGenerateColumns = false;
        registryValuesGrid.DataSource = _registryValues;
        InitializeRegistryTree();
        _manualAgents = _manualAgentStore.Load().ToList();
        _frequentPrograms = _frequentProgramStore.Load().ToList();
        groupFilterComboBox.Items.Add(TeacherClientText.AllGroups);
        groupFilterComboBox.SelectedIndex = 0;
        statusFilterComboBox.Items.AddRange([TeacherClientText.AllStatuses, TeacherClientText.Online, TeacherClientText.Offline, TeacherClientText.Unknown]);
        statusFilterComboBox.SelectedIndex = 0;
        autoReconnectCheckBox.Checked = true;
        PopulateLocalRoots();
        _ = LoadLocalDirectoryAsync(localPathTextBox.Text);

        _agentRefreshTimer.Interval = 15000;
        _agentRefreshTimer.Tick += async (_, _) => await LoadDiscoveredAgentsAsync(showBusyCursor: false);
        _connectionMonitorTimer.Interval = 10000;
        _connectionMonitorTimer.Tick += async (_, _) => await MonitorConnectionAsync();
        _updateStatusTimer.Interval = 5000;
        _updateStatusTimer.Tick += async (_, _) => await PollAgentUpdateStatusesAsync();
        Shown += async (_, _) =>
        {
            await LoadDiscoveredAgentsAsync();
            _agentRefreshTimer.Start();
            _connectionMonitorTimer.Start();
            _updateStatusTimer.Start();
        };
        FormClosing += (_, _) =>
        {
            _agentRefreshTimer.Stop();
            _connectionMonitorTimer.Stop();
            _updateStatusTimer.Stop();
            _updatePreparationService.Dispose();
            _clientUpdateService.Dispose();
        };
    }

    private static string GetUpdatePreparationRootDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var baseDirectory = string.IsNullOrWhiteSpace(localAppData)
            ? Path.Combine(AppContext.BaseDirectory, "data", "updates")
            : Path.Combine(localAppData, "TeacherServer", "TeacherClient", "updates");
        Directory.CreateDirectory(baseDirectory);
        return baseDirectory;
    }

    private static string GetClientUpdateRootDirectory()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var baseDirectory = string.IsNullOrWhiteSpace(localAppData)
            ? Path.Combine(AppContext.BaseDirectory, "data", "client-updates")
            : Path.Combine(localAppData, "TeacherServer", "TeacherClient", "client-updates");
        Directory.CreateDirectory(baseDirectory);
        return baseDirectory;
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
        await ApplyStudentPolicySettingsToOnlineAgentsAsync(reportSummary: true);
    }

    private async void refreshProcessesButton_Click(object sender, EventArgs e) => await LoadProcessesAsync();

    private void refreshRegistryButton_Click(object? sender, EventArgs e) => InitializeRegistryTree();

    private void registryTreeView_BeforeExpand(object? sender, TreeViewCancelEventArgs e)
    {
        if (e.Node?.Nodes.Count == 1 && e.Node.Nodes[0].Tag is string dummyTag && dummyTag == "dummy")
        {
            _ = LoadRegistrySubKeysAsync(e.Node);
        }
    }

    private async void registryTreeView_AfterSelect(object? sender, TreeViewEventArgs e)
    {
        if (e.Node?.Tag is not string path) return;
        await LoadRegistryValuesAsync(path);
    }

    private async Task LoadRegistryValuesAsync(string path)
    {
        try
        {
            var client = CreateClient();
            var values = await client.GetRegistryValuesAsync(path);
            _registryValues = new BindingList<RegistryValueDto>(values.ToList());
            registryValuesGrid.DataSource = _registryValues;
            SetStatus(TeacherClientText.FormatLoadedRegistryValues(values.Count));
        }
        catch (Exception ex)
        {
            SetStatus($"{TeacherClientText.RegistryLoadError}: {ex.Message}");
        }
    }

    private async Task LoadRegistrySubKeysAsync(TreeNode node)
    {
        if (node.Tag is not string path) return;
        try
        {
            var client = CreateClient();
            var subKeys = await client.GetRegistrySubKeysAsync(path);
            node.Nodes.Clear();
            foreach (var key in subKeys)
            {
                var childNode = new TreeNode(key.Name) { Tag = key.Path };
                if (key.HasChildren)
                    childNode.Nodes.Add(new TreeNode("...") { Tag = "dummy" });
                node.Nodes.Add(childNode);
            }
        }
        catch (Exception ex)
        {
            SetStatus($"{TeacherClientText.RegistryLoadError}: {ex.Message}");
            node.Nodes.Clear();
        }
    }

    private void InitializeRegistryTree()
    {
        registryTreeView.Nodes.Clear();
        _registryValues.Clear();
        string[] hives = ["HKEY_LOCAL_MACHINE", "HKEY_CURRENT_USER", "HKEY_CLASSES_ROOT", "HKEY_USERS", "HKEY_CURRENT_CONFIG"];
        foreach (var hive in hives)
        {
            var node = new TreeNode(hive) { Tag = hive };
            node.Nodes.Add(new TreeNode("...") { Tag = "dummy" });
            registryTreeView.Nodes.Add(node);
        }
    }

    private async void newRegistryValueButton_Click(object? sender, EventArgs e)
    {
        if (registryTreeView.SelectedNode?.Tag is not string path)
        {
            MessageBox.Show(this, TeacherClientText.SelectKeyFirst, TeacherClientText.Validation, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new RegistryEditDialog();
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            var client = CreateClient();
            await client.SetRegistryValueAsync(path, dialog.ValueName, dialog.ValueType, dialog.ValueData);
            SetStatus(TeacherClientText.ValueCreated);
            await LoadRegistryValuesAsync(path);
        }
        catch (Exception ex)
        {
            SetStatus($"{TeacherClientText.RegistryError}: {ex.Message}");
        }
    }

    private async void newRegistryKeyButton_Click(object? sender, EventArgs e)
    {
        if (registryTreeView.SelectedNode?.Tag is not string path)
        {
            MessageBox.Show(this, TeacherClientText.SelectKeyFirst, TeacherClientText.Validation, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new InputDialog(TeacherClientText.NewKey, TeacherClientText.KeyName);
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        try
        {
            var client = CreateClient();
            await client.CreateRegistryKeyAsync(path, dialog.Value);
            SetStatus(TeacherClientText.KeyCreated);
            _ = LoadRegistrySubKeysAsync(registryTreeView.SelectedNode);
        }
        catch (Exception ex)
        {
            SetStatus($"{TeacherClientText.RegistryError}: {ex.Message}");
        }
    }

    private async void editRegistryValueButton_Click(object? sender, EventArgs e)
    {
        if (registryValuesGrid.SelectedRows.Count == 0)
        {
            MessageBox.Show(this, TeacherClientText.SelectValueFirst, TeacherClientText.Validation, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (registryTreeView.SelectedNode?.Tag is not string path)
        {
            return;
        }

        if (registryValuesGrid.SelectedRows[0].DataBoundItem is not RegistryValueDto value)
        {
            return;
        }

        try
        {
            var client = CreateClient();
            var editableValue = (await client.GetRegistryValuesForEditAsync(path))
                .FirstOrDefault(x => string.Equals(x.Name, value.Name, StringComparison.Ordinal));
            if (editableValue is null)
            {
                SetStatus($"{TeacherClientText.RegistryError}: {TeacherClientText.SelectValueFirst}");
                return;
            }

            using var dialog = new RegistryEditDialog(editableValue.Name, editableValue.RawType, editableValue.RawData);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            await client.SetRegistryValueAsync(path, dialog.ValueName, dialog.ValueType, dialog.ValueData);
            SetStatus(TeacherClientText.ValueUpdated);
            await LoadRegistryValuesAsync(path);
        }
        catch (Exception ex)
        {
            SetStatus($"{TeacherClientText.RegistryError}: {ex.Message}");
        }
    }

    private async void exportRegistryKeyButton_Click(object? sender, EventArgs e)
    {
        if (registryTreeView.SelectedNode?.Tag is not string path)
        {
            MessageBox.Show(this, TeacherClientText.SelectKeyFirst, TeacherClientText.Validation, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = TeacherClientText.RegFilesFilter,
            DefaultExt = "reg",
            FileName = $"{path.Replace('\\', '_')}.reg"
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            var client = CreateClient();
            await client.ExportRegistryKeyAsync(path, dialog.FileName);
            SetStatus(TeacherClientText.ExportedRegistryKey(path));
        }
        catch (Exception ex)
        {
            SetStatus($"{TeacherClientText.RegistryExportError}: {ex.Message}");
        }
    }

    private async void importRegistryFileButton_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Filter = TeacherClientText.RegFilesFilter,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            var client = CreateClient();
            var result = await client.ImportRegistryFileAsync(dialog.FileName);
            if (registryTreeView.SelectedNode?.Tag is string path)
            {
                await LoadRegistrySubKeysAsync(registryTreeView.SelectedNode);
                await LoadRegistryValuesAsync(path);
            }
            else
            {
                InitializeRegistryTree();
            }

            SetStatus(TeacherClientText.ImportedRegistryFile(result?.KeysProcessed ?? 0, result?.ValuesProcessed ?? 0));
        }
        catch (Exception ex)
        {
            SetStatus($"{TeacherClientText.RegistryImportError}: {ex.Message}");
        }
    }

    private async void deleteRegistryValueButton_Click(object? sender, EventArgs e)
    {
        if (registryValuesGrid.SelectedRows.Count == 0)
        {
            MessageBox.Show(this, TeacherClientText.SelectValueFirst, TeacherClientText.Validation, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (registryTreeView.SelectedNode?.Tag is not string path)
        {
            return;
        }

        if (registryValuesGrid.SelectedRows[0].DataBoundItem is not RegistryValueDto value)
        {
            return;
        }

        if (MessageBox.Show(this, TeacherClientText.ConfirmDeleteValue, TeacherClientText.Confirmation, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        try
        {
            var client = CreateClient();
            await client.DeleteRegistryValueAsync(path, value.Name);
            SetStatus(TeacherClientText.ValueDeleted);
            await LoadRegistryValuesAsync(path);
        }
        catch (Exception ex)
        {
            SetStatus($"{TeacherClientText.RegistryError}: {ex.Message}");
        }
    }

    private async void deleteRegistryKeyButton_Click(object? sender, EventArgs e)
    {
        if (registryTreeView.SelectedNode?.Tag is not string path)
        {
            MessageBox.Show(this, TeacherClientText.SelectKeyFirst, TeacherClientText.Validation, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (MessageBox.Show(this, TeacherClientText.ConfirmDeleteKey, TeacherClientText.Confirmation, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
        {
            return;
        }

        try
        {
            var client = CreateClient();
            await client.DeleteRegistryKeyAsync(path);
            SetStatus(TeacherClientText.KeyDeleted);
            InitializeRegistryTree();
        }
        catch (Exception ex)
        {
            SetStatus($"{TeacherClientText.RegistryError}: {ex.Message}");
        }
    }

    private async void refreshAgentsButton_Click(object? sender, EventArgs e) => await LoadDiscoveredAgentsAsync();

    private async void connectSelectedAgentButton_Click(object? sender, EventArgs e)
    {
        await ConnectSelectedAgentAsync();
    }

    private async void saveDesktopIconLayoutMenuItem_Click(object? sender, EventArgs e)
    {
        await SaveDesktopIconLayoutAsync();
    }

    private async void restoreDesktopIconLayoutMenuItem_Click(object? sender, EventArgs e)
    {
        await RestoreDesktopIconLayoutAsync();
    }

    private async void checkSelectedAgentUpdateButton_Click(object? sender, EventArgs e)
    {
        await CheckSelectedAgentUpdateAsync();
    }

    private async void startSelectedAgentUpdateButton_Click(object? sender, EventArgs e)
    {
        await StartSelectedAgentUpdateAsync();
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

    private async void processesGrid_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || processesGrid.Rows[e.RowIndex].DataBoundItem is not ProcessInfoDto process)
        {
            return;
        }

        try
        {
            using var cursorScope = new CursorScope(this);
            var client = CreateClient();
            var details = await client.GetProcessDetailsAsync(process.Id);
            if (details is null)
            {
                SetStatus(TeacherClientText.ProcessDetailsLoadError);
                return;
            }

            using var dialog = new ProcessDetailsDialog(details);
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            if (dialog.ActionRequested == ProcessActionRequested.Kill)
            {
                if (MessageBox.Show(
                        TeacherClientText.TerminateProcessPrompt(process.Name, process.Id),
                        TeacherClientText.TerminateProcessTitle,
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning) != DialogResult.Yes)
                {
                    return;
                }

                await client.KillProcessAsync(process.Id);
                await LoadProcessesAsync();
                SetStatus(TeacherClientText.FormatProcessTerminated(process.Name));
            }
            else if (dialog.ActionRequested == ProcessActionRequested.Restart)
            {
                if (MessageBox.Show(
                        TeacherClientText.RestartProcessPrompt(process.Name, process.Id),
                        TeacherClientText.RestartCommand,
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Warning) != DialogResult.Yes)
                {
                    return;
                }

                await client.RestartProcessAsync(process.Id);
                await LoadProcessesAsync();
                SetStatus(TeacherClientText.FormatProcessRestarted(process.Name));
            }
        }
        catch (Exception ex)
        {
            SetStatus($"{TeacherClientText.ProcessDetailsLoadError}: {ex.Message}");
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
                : BuildAgentAvailabilityStatus(discoveredAgents.Count, _manualAgents.Count));
        }
        catch (Exception ex)
        {
            SetStatus($"{TeacherClientText.DiscoveryError}: {ex.Message}");
        }
    }

    private async Task RefreshFrequentProgramsAsync()
    {
        var targetAgents = _allAgents
            .Where(x => string.Equals(x.Status, TeacherClientText.Online, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (targetAgents.Count == 0)
        {
            SetStatus(TeacherClientText.NoOnlineAgentsAvailableForGroupCommand);
            return;
        }

        var collected = new List<FrequentProgramEntry>(_frequentPrograms);
        var failures = new List<string>();

        try
        {
            using var cursorScope = new CursorScope(this);
            for (var index = 0; index < targetAgents.Count; index++)
            {
                var agent = targetAgents[index];
                SetStatus(TeacherClientText.RemoteCommandProgress(agent.MachineName, index + 1, targetAgents.Count));

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
            SetStatus(TeacherClientText.FrequentProgramsRefreshed(_frequentPrograms.Count));

            if (failures.Count > 0)
            {
                MessageBox.Show(
                    this,
                    string.Join(Environment.NewLine, failures),
                    TeacherClientText.FrequentProgramsRefreshError,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            SetStatus($"{TeacherClientText.FrequentProgramsRefreshError}: {ex.Message}");
        }
    }

    private async Task ExecuteRemoteCommandOnAgentsAsync(IReadOnlyList<DiscoveredAgentRow> targetAgents, bool selectedOnly)
    {
        using var dialog = new RemoteCommandDialog(_frequentPrograms);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        if (MessageBox.Show(
                TeacherClientText.RemoteCommandPrompt(targetAgents.Count, selectedOnly),
                TeacherClientText.GroupCommandsMenu,
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        var failures = new List<string>();
        var succeeded = 0;

        try
        {
            using var cursorScope = new CursorScope(this);
            for (var index = 0; index < targetAgents.Count; index++)
            {
                var agent = targetAgents[index];

                try
                {
                    SetStatus(TeacherClientText.RemoteCommandProgress(agent.MachineName, index + 1, targetAgents.Count));
                    var client = new TeacherApiClient($"http://{agent.RespondingAddress}:{agent.Port}", _clientSettings.SharedSecret);
                    await client.ExecuteRemoteCommandAsync(dialog.Script, dialog.RunAs);
                    succeeded++;
                }
                catch (Exception ex)
                {
                    failures.Add($"{agent.MachineName}: {ex.Message}");
                }
            }

            SetStatus(
                failures.Count == 0
                    ? TeacherClientText.RemoteCommandCompleted(succeeded)
                    : TeacherClientText.RemoteCommandCompletedWithFailures(succeeded, failures.Count));

            if (failures.Count > 0)
            {
                MessageBox.Show(
                    this,
                    string.Join(Environment.NewLine, failures),
                    TeacherClientText.BulkCommandsResultTitle,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
        catch (Exception ex)
        {
            SetStatus($"{TeacherClientText.BulkCommandsResultTitle}: {ex.Message}");
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

    private async void runCommandOnSelectedStudentsMenuItem_Click(object? sender, EventArgs e)
    {
        var targetAgents = GetSelectedAgents();
        if (targetAgents.Count == 0)
        {
            SetStatus(TeacherClientText.ChooseAgentsForDistribution);
            return;
        }

        await ExecuteRemoteCommandOnAgentsAsync(targetAgents, selectedOnly: true);
    }

    private async void runCommandOnAllOnlineStudentsMenuItem_Click(object? sender, EventArgs e)
    {
        var targetAgents = _allAgents
            .Where(x => string.Equals(x.Status, TeacherClientText.Online, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (targetAgents.Count == 0)
        {
            SetStatus(TeacherClientText.NoOnlineAgentsAvailableForGroupCommand);
            return;
        }

        await ExecuteRemoteCommandOnAgentsAsync(targetAgents, selectedOnly: false);
    }

    private async void updateSelectedStudentsMenuItem_Click(object? sender, EventArgs e)
    {
        var targetAgents = GetSelectedAgents();
        if (targetAgents.Count == 0)
        {
            SetStatus(TeacherClientText.ChooseAgentsForDistribution);
            return;
        }

        await StartAgentUpdateOnAgentsAsync(targetAgents, selectedOnly: true);
    }

    private async void updateAllOnlineStudentsMenuItem_Click(object? sender, EventArgs e)
    {
        var targetAgents = _allAgents
            .Where(x => string.Equals(x.Status, TeacherClientText.Online, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (targetAgents.Count == 0)
        {
            SetStatus(TeacherClientText.NoOnlineAgentsAvailableForGroupCommand);
            return;
        }

        await StartAgentUpdateOnAgentsAsync(targetAgents, selectedOnly: false);
    }

    private async void restoreDesktopIconsOnSelectedStudentsMenuItem_Click(object? sender, EventArgs e)
    {
        var targetAgents = GetSelectedAgents();
        if (targetAgents.Count == 0)
        {
            SetStatus(TeacherClientText.ChooseAgentsForDistribution);
            return;
        }

        await RestoreDesktopIconsOnAgentsAsync(FilterOutCurrentConnectedAgent(targetAgents), selectedOnly: true);
    }

    private async void restoreDesktopIconsOnAllOnlineStudentsMenuItem_Click(object? sender, EventArgs e)
    {
        var targetAgents = _allAgents
            .Where(x => string.Equals(x.Status, TeacherClientText.Online, StringComparison.OrdinalIgnoreCase))
            .ToList();

        await RestoreDesktopIconsOnAgentsAsync(FilterOutCurrentConnectedAgent(targetAgents), selectedOnly: false);
    }

    private async void applyCurrentDesktopLayoutToSelectedStudentsMenuItem_Click(object? sender, EventArgs e)
    {
        var targetAgents = GetSelectedAgents();
        if (targetAgents.Count == 0)
        {
            SetStatus(TeacherClientText.ChooseAgentsForDistribution);
            return;
        }

        await ApplyCurrentDesktopLayoutToAgentsAsync(FilterOutCurrentConnectedAgent(targetAgents), selectedOnly: true);
    }

    private async void applyCurrentDesktopLayoutToAllOnlineStudentsMenuItem_Click(object? sender, EventArgs e)
    {
        var targetAgents = _allAgents
            .Where(x => string.Equals(x.Status, TeacherClientText.Online, StringComparison.OrdinalIgnoreCase))
            .ToList();

        await ApplyCurrentDesktopLayoutToAgentsAsync(FilterOutCurrentConnectedAgent(targetAgents), selectedOnly: false);
    }

    private async void refreshFrequentProgramsMenuItem_Click(object? sender, EventArgs e)
    {
        await RefreshFrequentProgramsAsync();
    }

    private void manageFrequentProgramsMenuItem_Click(object? sender, EventArgs e)
    {
        using var dialog = new FrequentProgramsDialog(_frequentPrograms);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _frequentPrograms = dialog.Entries.ToList();
        _frequentProgramStore.Save(_frequentPrograms);
        SetStatus(TeacherClientText.FrequentProgramsRefreshed(_frequentPrograms.Count));
    }

    private async void shutdownSelectedStudentsMenuItem_Click(object? sender, EventArgs e)
    {
        var targetAgents = GetSelectedAgents();
        if (targetAgents.Count == 0)
        {
            SetStatus(TeacherClientText.ChooseAgentsForDistribution);
            return;
        }

        await ExecutePowerActionOnAgentsAsync(targetAgents, PowerActionKind.Shutdown, selectedOnly: true);
    }

    private async void restartSelectedStudentsMenuItem_Click(object? sender, EventArgs e)
    {
        var targetAgents = GetSelectedAgents();
        if (targetAgents.Count == 0)
        {
            SetStatus(TeacherClientText.ChooseAgentsForDistribution);
            return;
        }

        await ExecutePowerActionOnAgentsAsync(targetAgents, PowerActionKind.Restart, selectedOnly: true);
    }

    private async void logOffSelectedStudentsMenuItem_Click(object? sender, EventArgs e)
    {
        var targetAgents = GetSelectedAgents();
        if (targetAgents.Count == 0)
        {
            SetStatus(TeacherClientText.ChooseAgentsForDistribution);
            return;
        }

        await ExecutePowerActionOnAgentsAsync(targetAgents, PowerActionKind.LogOff, selectedOnly: true);
    }

    private async void shutdownAllOnlineStudentsMenuItem_Click(object? sender, EventArgs e)
    {
        var targetAgents = _allAgents.Where(x => string.Equals(x.Status, TeacherClientText.Online, StringComparison.OrdinalIgnoreCase)).ToList();
        if (targetAgents.Count == 0)
        {
            SetStatus(TeacherClientText.NoOnlineAgentsAvailableForGroupCommand);
            return;
        }

        await ExecutePowerActionOnAgentsAsync(targetAgents, PowerActionKind.Shutdown, selectedOnly: false);
    }

    private async void restartAllOnlineStudentsMenuItem_Click(object? sender, EventArgs e)
    {
        var targetAgents = _allAgents.Where(x => string.Equals(x.Status, TeacherClientText.Online, StringComparison.OrdinalIgnoreCase)).ToList();
        if (targetAgents.Count == 0)
        {
            SetStatus(TeacherClientText.NoOnlineAgentsAvailableForGroupCommand);
            return;
        }

        await ExecutePowerActionOnAgentsAsync(targetAgents, PowerActionKind.Restart, selectedOnly: false);
    }

    private async void logOffAllOnlineStudentsMenuItem_Click(object? sender, EventArgs e)
    {
        var targetAgents = _allAgents.Where(x => string.Equals(x.Status, TeacherClientText.Online, StringComparison.OrdinalIgnoreCase)).ToList();
        if (targetAgents.Count == 0)
        {
            SetStatus(TeacherClientText.NoOnlineAgentsAvailableForGroupCommand);
            return;
        }

        await ExecutePowerActionOnAgentsAsync(targetAgents, PowerActionKind.LogOff, selectedOnly: false);
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
            UpdateLocalDriveSpace(info.FullName);
            _localEntries = new BindingList<FileSystemEntryDto>(entries);
            localFilesGrid.DataSource = _localEntries;
        }
        catch (Exception ex)
        {
            localDriveSpaceLabel.Text = TeacherClientText.DriveFreeSpaceUnknown;
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
            await UpdateRemoteDriveSpaceAsync(client, listing.CurrentPath);
            _remoteParentPath = listing.ParentPath;
            _remoteEntries = new BindingList<FileSystemEntryDto>(listing.Entries.ToList());
            remoteFilesGrid.DataSource = _remoteEntries;
        }
        catch (Exception ex)
        {
            remoteDriveSpaceLabel.Text = TeacherClientText.DriveFreeSpaceUnknown;
            SetStatus($"{TeacherClientText.RemoteBrowseError}: {ex.Message}");
        }
    }

    private void UpdateLocalDriveSpace(string? path)
    {
        try
        {
            var resolvedPath = string.IsNullOrWhiteSpace(path)
                ? (localDriveComboBox.SelectedItem?.ToString() ?? Directory.GetLogicalDrives().FirstOrDefault())
                : path;
            if (string.IsNullOrWhiteSpace(resolvedPath))
            {
                localDriveSpaceLabel.Text = TeacherClientText.DriveFreeSpaceUnknown;
                return;
            }

            var root = Path.GetPathRoot(resolvedPath);
            if (string.IsNullOrWhiteSpace(root))
            {
                localDriveSpaceLabel.Text = TeacherClientText.DriveFreeSpaceUnknown;
                return;
            }

            var drive = new DriveInfo(root);
            localDriveSpaceLabel.Text = drive.IsReady
                ? TeacherClientText.DriveFreeSpace(FormatByteSize(drive.AvailableFreeSpace), FormatByteSize(drive.TotalSize))
                : TeacherClientText.DriveFreeSpaceUnknown;
        }
        catch
        {
            localDriveSpaceLabel.Text = TeacherClientText.DriveFreeSpaceUnknown;
        }
    }

    private async Task UpdateRemoteDriveSpaceAsync(TeacherApiClient client, string? path)
    {
        try
        {
            var space = await client.GetRemoteDriveSpaceAsync(path);
            remoteDriveSpaceLabel.Text = space is null
                ? TeacherClientText.DriveFreeSpaceUnknown
                : TeacherClientText.DriveFreeSpace(FormatByteSize(space.AvailableBytes), FormatByteSize(space.TotalBytes));
        }
        catch
        {
            remoteDriveSpaceLabel.Text = TeacherClientText.DriveFreeSpaceUnknown;
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
        if (e.RowIndex < 0 || localFilesGrid.Rows[e.RowIndex].DataBoundItem is not FileSystemEntryDto entry)
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

    private async void remoteFilesGrid_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || remoteFilesGrid.Rows[e.RowIndex].DataBoundItem is not FileSystemEntryDto entry)
        {
            return;
        }

        if (entry.IsDirectory)
        {
            await LoadRemoteDirectoryAsync(entry.FullPath);
            return;
        }

        openRemoteButton_Click(sender, EventArgs.Empty);
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
            var progress = new Progress<TeacherApiClient.TransferProgress>(value =>
                SetStatus(BuildTransferStatus(TeacherClientText.Upload, entry.Name, value)));
            await client.UploadFileAsync(entry.FullPath, remotePathTextBox.Text, progress);
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
            var progress = new Progress<TeacherApiClient.TransferProgress>(value =>
                SetStatus(BuildTransferStatus(TeacherClientText.Download, entry.Name, value)));
            await client.DownloadRemoteFileAsync(entry.FullPath, localPathTextBox.Text, progress);
            await LoadLocalDirectoryAsync(localPathTextBox.Text);
            SetStatus(TeacherClientText.FormatDownloaded(entry.Name));
        }
        catch (Exception ex)
        {
            SetStatus($"{TeacherClientText.DownloadError}: {ex.Message}");
        }
    }

    private async void openRemoteButton_Click(object? sender, EventArgs e)
    {
        if (remoteFilesGrid.CurrentRow?.DataBoundItem is not FileSystemEntryDto entry)
        {
            SetStatus(TeacherClientText.ChooseRemoteEntryFirst);
            return;
        }

        try
        {
            using var cursorScope = new CursorScope(this);
            var client = CreateClient();
            await client.OpenRemoteEntryAsync(entry.FullPath);
            SetStatus(TeacherClientText.FormatOpenedRemote(entry.Name));
        }
        catch (Exception ex)
        {
            SetStatus($"{TeacherClientText.OpenRemoteError}: {ex.Message}");
        }
    }

    private void openLocalButton_Click(object? sender, EventArgs e)
    {
        if (localFilesGrid.CurrentRow?.DataBoundItem is not FileSystemEntryDto entry)
        {
            SetStatus(TeacherClientText.ChooseLocalEntryFirst);
            return;
        }

        OpenLocalEntry(entry);
    }

    private async void renameLocalButton_Click(object? sender, EventArgs e)
    {
        if (localFilesGrid.CurrentRow?.DataBoundItem is not FileSystemEntryDto entry)
        {
            SetStatus(TeacherClientText.ChooseLocalEntryFirst);
            return;
        }

        using var dialog = new InputDialog(TeacherClientText.RenameLocalEntryTitle, TeacherClientText.EntryName, entry.Name);
        if (dialog.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(dialog.Value))
        {
            return;
        }

        try
        {
            using var cursorScope = new CursorScope(this);
            RenameLocalEntry(entry, dialog.Value);
            await LoadLocalDirectoryAsync(localPathTextBox.Text);
            SetStatus(TeacherClientText.FormatRenamedLocal(entry.Name, dialog.Value.Trim()));
        }
        catch (Exception ex)
        {
            SetStatus($"{TeacherClientText.LocalRenameError}: {ex.Message}");
        }
    }

    private async void renameRemoteButton_Click(object? sender, EventArgs e)
    {
        if (remoteFilesGrid.CurrentRow?.DataBoundItem is not FileSystemEntryDto entry)
        {
            SetStatus(TeacherClientText.ChooseRemoteEntryFirst);
            return;
        }

        using var dialog = new InputDialog(TeacherClientText.RenameRemoteEntryTitle, TeacherClientText.EntryName, entry.Name);
        if (dialog.ShowDialog(this) != DialogResult.OK || string.IsNullOrWhiteSpace(dialog.Value))
        {
            return;
        }

        try
        {
            using var cursorScope = new CursorScope(this);
            var client = CreateClient();
            await client.RenameRemoteEntryAsync(entry.FullPath, dialog.Value.Trim());
            await LoadRemoteDirectoryAsync(remotePathTextBox.Text);
            SetStatus(TeacherClientText.FormatRenamedRemote(entry.Name, dialog.Value.Trim()));
        }
        catch (Exception ex)
        {
            SetStatus($"{TeacherClientText.RemoteRenameError}: {ex.Message}");
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

    private void OpenLocalEntry(FileSystemEntryDto entry)
    {
        try
        {
            using var cursorScope = new CursorScope(this);
            Process.Start(new ProcessStartInfo
            {
                FileName = entry.FullPath,
                UseShellExecute = true
            });
            SetStatus(TeacherClientText.FormatOpenedLocal(entry.Name));
        }
        catch (Exception ex)
        {
            SetStatus($"{TeacherClientText.OpenLocalError}: {ex.Message}");
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

    private void aboutMenuItem_Click(object? sender, EventArgs e)
    {
        using var dialog = new AboutDialog();
        dialog.ShowDialog(this);
    }

    private void checkClientUpdateMenuItem_Click(object? sender, EventArgs e)
    {
        using var dialog = new ClientUpdateDialog(_clientUpdateService);
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

    private async Task CheckSelectedAgentUpdateAsync()
    {
        using var dialog = new UpdatePreparationDialog(_updatePreparationService);
        dialog.ShowDialog(this);
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
        if (agentsGrid.CurrentRow?.DataBoundItem is not DiscoveredAgentRow agent)
        {
            SetStatus(TeacherClientText.ChooseAgentFirst);
            return;
        }

        if (!string.Equals(agent.Status, TeacherClientText.Online, StringComparison.OrdinalIgnoreCase))
        {
            SetStatus(TeacherClientText.AgentUpdateRequiresOnlineAgent);
            return;
        }

        try
        {
            var client = new TeacherApiClient($"http://{agent.RespondingAddress}:{agent.Port}", _clientSettings.SharedSecret);
            var request = await CreatePreparedUpdateRequestAsync(agent, client);
            if (request is null)
            {
                SetStatus(TeacherClientText.UpdatePreparationMissing);
                return;
            }
            var status = await client.StartAgentUpdateAsync(request);
            if (status is not null)
            {
                ReplaceAgentRow(ApplyUpdateStatus(agent, status));
            }
            SetStatus(TeacherClientText.AgentUpdateStarted(agent.MachineName, status?.AvailableVersion ?? "?"));
        }
        catch (Exception ex)
        {
            SetStatus($"{TeacherClientText.AgentUpdateStartFailed}: {ex.Message}");
        }
    }

    private async Task RunDesktopIconLayoutActionAsync(
        bool save,
        Func<TeacherApiClient, Task<DesktopIconLayoutOperationResultDto?>> execute)
    {
        if (string.IsNullOrWhiteSpace(_lastConnectedServerUrl))
        {
            SetStatus(TeacherClientText.ConnectFromAgentsTabFirst);
            return;
        }

        using var cursorScope = new CursorScope(this);
        try
        {
            var client = CreateClient();
            var result = await execute(client);
            var machineName = _lastConnectedMachineName ?? "PC";
            var iconCount = result?.IconCount ?? 0;
            SetStatus(save
                ? TeacherClientText.DesktopIconLayoutSaved(machineName, iconCount)
                : TeacherClientText.DesktopIconLayoutRestored(machineName, iconCount));
        }
        catch (Exception ex)
        {
            SetStatus($"{TeacherClientText.DesktopIconLayoutError}: {ex.Message}");
        }
    }

    private async Task RestoreDesktopIconsOnAgentsAsync(IReadOnlyList<DiscoveredAgentRow> targetAgents, bool selectedOnly)
    {
        if (targetAgents.Count == 0)
        {
            SetStatus(TeacherClientText.NoOnlineAgentsAvailableForGroupCommand);
            return;
        }

        var failures = new List<string>();
        var succeeded = 0;
        using var cursorScope = new CursorScope(this);

        for (var index = 0; index < targetAgents.Count; index++)
        {
            var agent = targetAgents[index];
            try
            {
                SetStatus(TeacherClientText.DesktopIconLayoutBulkProgress(agent.MachineName, index + 1, targetAgents.Count));
                var client = new TeacherApiClient($"http://{agent.RespondingAddress}:{agent.Port}", _clientSettings.SharedSecret);
                await client.RestoreDesktopIconLayoutAsync();
                succeeded++;
            }
            catch (Exception ex)
            {
                failures.Add($"{agent.MachineName}: {ex.Message}");
            }
        }

        SetStatus(failures.Count == 0
            ? TeacherClientText.DesktopIconLayoutBulkCompleted(succeeded)
            : TeacherClientText.DesktopIconLayoutBulkCompletedWithFailures(succeeded, failures.Count));

        if (failures.Count > 0)
        {
            MessageBox.Show(
                this,
                string.Join(Environment.NewLine, failures),
                TeacherClientText.DesktopIconLayoutError,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private async Task ApplyCurrentDesktopLayoutToAgentsAsync(IReadOnlyList<DiscoveredAgentRow> targetAgents, bool selectedOnly)
    {
        if (string.IsNullOrWhiteSpace(_lastConnectedServerUrl))
        {
            SetStatus(TeacherClientText.ConnectFromAgentsTabFirst);
            return;
        }

        if (targetAgents.Count == 0)
        {
            SetStatus(TeacherClientText.NoOnlineAgentsAvailableForGroupCommand);
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
            SetStatus($"{TeacherClientText.DesktopIconLayoutError}: {ex.Message}");
            return;
        }

        var failures = new List<string>();
        var succeeded = 0;
        using var cursorScope = new CursorScope(this);

        for (var index = 0; index < targetAgents.Count; index++)
        {
            var agent = targetAgents[index];
            try
            {
                SetStatus(TeacherClientText.DesktopIconLayoutApplyBulkProgress(agent.MachineName, index + 1, targetAgents.Count));
                var client = new TeacherApiClient($"http://{agent.RespondingAddress}:{agent.Port}", _clientSettings.SharedSecret);
                await client.ApplyDesktopIconLayoutAsync(sourceLayout, restoreAfterApply: true);
                succeeded++;
            }
            catch (Exception ex)
            {
                failures.Add($"{agent.MachineName}: {ex.Message}");
            }
        }

        SetStatus(failures.Count == 0
            ? TeacherClientText.DesktopIconLayoutAppliedBulkCompleted(succeeded)
            : TeacherClientText.DesktopIconLayoutAppliedBulkCompletedWithFailures(succeeded, failures.Count));

        if (failures.Count > 0)
        {
            MessageBox.Show(
                this,
                string.Join(Environment.NewLine, failures),
                TeacherClientText.DesktopIconLayoutError,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private List<DiscoveredAgentRow> FilterOutCurrentConnectedAgent(IEnumerable<DiscoveredAgentRow> agents)
    {
        return agents
            .Where(x => string.Equals(x.Status, TeacherClientText.Online, StringComparison.OrdinalIgnoreCase))
            .Where(x => string.IsNullOrWhiteSpace(_lastConnectedAgentId)
                || !string.Equals(x.AgentId, _lastConnectedAgentId, StringComparison.OrdinalIgnoreCase))
            .ToList();
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

    private async Task StartAgentUpdateOnAgentsAsync(IReadOnlyList<DiscoveredAgentRow> targetAgents, bool selectedOnly)
    {
        if (MessageBox.Show(
                TeacherClientText.BulkAgentUpdatePrompt(targetAgents.Count, selectedOnly),
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
                    SetStatus(TeacherClientText.BulkAgentUpdateProgress(agent.MachineName, agentIndex + 1, targetAgents.Count));
                    var client = new TeacherApiClient($"http://{agent.RespondingAddress}:{agent.Port}", _clientSettings.SharedSecret);
                    var request = await CreatePreparedUpdateRequestAsync(agent, client);
                    if (request is null)
                    {
                        throw new InvalidOperationException(TeacherClientText.UpdatePreparationMissing);
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
            ? TeacherClientText.BulkAgentUpdateCompleted(succeeded)
            : TeacherClientText.BulkAgentUpdateCompletedWithFailures(succeeded, failures.Count));

        if (failures.Count > 0)
        {
            MessageBox.Show(
                string.Join(Environment.NewLine, failures),
                TeacherClientText.BulkCommandsResultTitle,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
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
                await DownloadRemoteDirectoryContentsAsync(client, studentWorkPath, localWorkFolder, SetStatus, agent.MachineName);
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
                            TeacherClientText.Download,
                            agentName ?? string.Empty,
                            entry.Name,
                            value)));
                await client.DownloadRemoteFileAsync(entry.FullPath, localDestinationDirectory, progress, cancellationToken);
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

    private static string BuildTransferStatus(string operation, string fileName, TeacherApiClient.TransferProgress progress)
    {
        var transferred = FormatByteSize(progress.BytesTransferred);
        if (!progress.HasTotal)
        {
            return $"{operation}: {fileName} ({transferred})";
        }

        return $"{operation}: {fileName} ({progress.Percent}% · {transferred} / {FormatByteSize(progress.TotalBytes!.Value)})";
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
            : $"{operation}: {agentName}";
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
            var progress = new Progress<TeacherApiClient.TransferProgress>(value =>
                reportStatus(BuildBulkTransferStatus(
                    TeacherClientText.Upload,
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

    private void SetStatus(string text) => statusLabel.Text = text;

    private async Task ConnectToServerAsync(string serverUrl, DiscoveredAgentRow? agent, string sourceLabel)
    {
        using var cursorScope = new CursorScope(this);
        try
        {
            var client = new TeacherApiClient(serverUrl, _clientSettings.SharedSecret);
            var info = await client.GetServerInfoAsync();
            if (info is null)
            {
                SetStatus(TeacherClientText.ConnectionFailed);
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
            SetStatus(TeacherClientText.FormatConnectedToAgent(sourceLabel, info.MachineName, NormalizeUserDisplay(info.CurrentUser, info.MachineName), info.AgentVersion));
            await LoadProcessesAsync();
            await LoadLocalDirectoryAsync(localPathTextBox.Text);
            await LoadRemoteDirectoryAsync(remotePathTextBox.Text);
        }
        catch (Exception ex)
        {
            SetStatus($"{TeacherClientText.ConnectionFailed} {ex.Message}");
        }
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
            .Where(x => string.Equals(x.Status, TeacherClientText.Online, StringComparison.OrdinalIgnoreCase))
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

        if (failed == 0)
        {
            SetStatus(TeacherClientText.StudentPolicySettingsApplied(succeeded));
            return;
        }

        SetStatus(TeacherClientText.StudentPolicySettingsAppliedWithFailures(succeeded, failed));
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
            .Where(x => string.Equals(x.Status, TeacherClientText.Online, StringComparison.OrdinalIgnoreCase))
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
            UpdateStatusBadge = TeacherClientText.UpdateStateBadge(status)
        };
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
                        var updateStatus = await reachabilityClient.GetUpdateStatusAsync();
                        updatedAgents.Add(agent with
                        {
                            Status = TeacherClientText.Online,
                            CurrentUser = NormalizeUserDisplay(info.CurrentUser, info.MachineName),
                            BrowserLockEnabled = info.IsBrowserLockEnabled,
                            InputLockEnabled = info.IsInputLockEnabled,
                            UpdateStatusBadge = TeacherClientText.UpdateStateBadge(updateStatus),
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

                updatedAgents.Add(agent with { Status = TeacherClientText.Online });
                continue;
            }

            var isReachable = await reachabilityClient.IsServerReachableAsync();
            if (isReachable)
            {
                try
                {
                    var info = await reachabilityClient.GetServerInfoAsync();
                    var updateStatus = await reachabilityClient.GetUpdateStatusAsync();
                    updatedAgents.Add(agent with
                    {
                        Status = TeacherClientText.Online,
                        CurrentUser = info is null ? agent.CurrentUser : NormalizeUserDisplay(info.CurrentUser, info.MachineName),
                        BrowserLockEnabled = info?.IsBrowserLockEnabled ?? agent.BrowserLockEnabled,
                        InputLockEnabled = info?.IsInputLockEnabled ?? agent.InputLockEnabled,
                        UpdateStatusBadge = TeacherClientText.UpdateStateBadge(updateStatus),
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
        string UpdateStatusBadge,
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
                NormalizeUserDisplay(dto.CurrentUser, dto.MachineName),
                dto.RespondingAddress,
                dto.Port,
                string.Join(", ", dto.MacAddresses),
                string.Empty,
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
                string.Empty,
                TeacherClientText.ManualVersion,
                DateTime.MinValue,
                true);
        }
    }

    private string BuildAgentAvailabilityStatus(int discoveredCount, int manualCount)
    {
        return string.IsNullOrWhiteSpace(_lastConnectedMachineName)
            ? TeacherClientText.FormatAvailableAgents(_allAgents.Count, discoveredCount, manualCount)
            : TeacherClientText.FormatAvailableAgentsWithConnected(_allAgents.Count, discoveredCount, manualCount, _lastConnectedMachineName);
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

    private async Task ExecutePowerActionOnAgentsAsync(IReadOnlyList<DiscoveredAgentRow> targetAgents, PowerActionKind action, bool selectedOnly)
    {
        if (MessageBox.Show(
                TeacherClientText.PowerActionPrompt(action, targetAgents.Count, selectedOnly),
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
                SetStatus(TeacherClientText.PowerActionProgress(action, agent.MachineName, agentIndex + 1, targetAgents.Count));
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
            ? TeacherClientText.PowerActionCompleted(action, succeeded)
            : TeacherClientText.PowerActionCompletedWithFailures(action, succeeded, failures.Count));

        if (failures.Count > 0)
        {
            MessageBox.Show(
                string.Join(Environment.NewLine, failures),
                TeacherClientText.BulkPowerActionError(action),
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }
}
