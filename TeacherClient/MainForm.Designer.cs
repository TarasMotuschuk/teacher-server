#nullable enable

using System.Drawing.Drawing2D;
using TeacherClient.Localization;

namespace TeacherClient;

partial class MainForm
{
    private System.ComponentModel.IContainer? components = null;
    private MenuStrip mainMenuStrip = null!;
    private TabControl mainTabControl = null!;
    private TabPage agentsTabPage = null!;
    private TabPage processesTabPage = null!;
    private TabPage filesTabPage = null!;
    private TabPage registryTabPage = null!;
    private TreeView registryTreeView = null!;
    private DataGridView registryValuesGrid = null!;
    private Button settingsButton = null!;
    private Label statusLabel = null!;
    private DataGridView processesGrid = null!;
    private Button refreshProcessesButton = null!;
    private Button killProcessButton = null!;
    private DataGridView localFilesGrid = null!;
    private DataGridView remoteFilesGrid = null!;
    private ComboBox localDriveComboBox = null!;
    private ComboBox remoteDriveComboBox = null!;
    private TextBox localPathTextBox = null!;
    private TextBox remotePathTextBox = null!;
    private Button refreshFilesButton = null!;
    private Button uploadButton = null!;
    private Button downloadButton = null!;
    private Button openRemoteButton = null!;
    private Button deleteLocalButton = null!;
    private Button deleteRemoteButton = null!;
    private Button newRemoteFolderButton = null!;
    private Button upLocalButton = null!;
    private Button upRemoteButton = null!;
    private DataGridView agentsGrid = null!;
    private Button refreshAgentsButton = null!;
    private Button connectSelectedAgentButton = null!;
    private Button addManualAgentButton = null!;
    private Button editManualAgentButton = null!;
    private Button removeManualAgentButton = null!;
    private TextBox agentSearchTextBox = null!;
    private ComboBox groupFilterComboBox = null!;
    private ComboBox statusFilterComboBox = null!;
    private CheckBox autoReconnectCheckBox = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        mainMenuStrip = new MenuStrip();
        mainTabControl = new TabControl();
        agentsTabPage = new TabPage();
        processesTabPage = new TabPage();
        filesTabPage = new TabPage();
        registryTabPage = new TabPage();
        registryTreeView = new TreeView();
        registryValuesGrid = new DataGridView();
        settingsButton = new Button();
        statusLabel = new Label();
        processesGrid = new DataGridView();
        refreshProcessesButton = new Button();
        killProcessButton = new Button();
        localFilesGrid = new DataGridView();
        remoteFilesGrid = new DataGridView();
        localDriveComboBox = new ComboBox();
        remoteDriveComboBox = new ComboBox();
        localPathTextBox = new TextBox();
        remotePathTextBox = new TextBox();
        refreshFilesButton = new Button();
        uploadButton = new Button();
        downloadButton = new Button();
        openRemoteButton = new Button();
        deleteLocalButton = new Button();
        deleteRemoteButton = new Button();
        newRemoteFolderButton = new Button();
        upLocalButton = new Button();
        upRemoteButton = new Button();
        agentsGrid = new DataGridView();
        refreshAgentsButton = new Button();
        connectSelectedAgentButton = new Button();
        addManualAgentButton = new Button();
        editManualAgentButton = new Button();
        removeManualAgentButton = new Button();
        agentSearchTextBox = new TextBox();
        groupFilterComboBox = new ComboBox();
        statusFilterComboBox = new ComboBox();
        autoReconnectCheckBox = new CheckBox();
        SuspendLayout();

        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.FromArgb(236, 239, 243);
        Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        Text = TeacherClientText.MainTitle;
        MinimumSize = new Size(1280, 840);
        WindowState = FormWindowState.Maximized;
        MainMenuStrip = mainMenuStrip;

        var connectionMenuItem = new ToolStripMenuItem(TeacherClientText.ConnectionMenu);
        connectionMenuItem.DropDownItems.Add(TeacherClientText.Settings, null, settingsButton_Click);
        connectionMenuItem.DropDownItems.Add(TeacherClientText.RefreshAgents, null, refreshAgentsButton_Click);
        connectionMenuItem.DropDownItems.Add(TeacherClientText.ConnectSelectedAgent, null, connectSelectedAgentButton_Click);
        connectionMenuItem.DropDownItems.Add(TeacherClientText.AddManualAgent, null, addManualAgentButton_Click);
        connectionMenuItem.DropDownItems.Add(TeacherClientText.EditManualAgent, null, editManualAgentButton_Click);
        connectionMenuItem.DropDownItems.Add(TeacherClientText.RemoveManualAgent, null, removeManualAgentButton_Click);

        var processesMenuItem = new ToolStripMenuItem(TeacherClientText.ProcessesMenu);
        processesMenuItem.DropDownItems.Add(TeacherClientText.Refresh, null, refreshProcessesButton_Click);
        processesMenuItem.DropDownItems.Add(TeacherClientText.TerminateSelected, null, killProcessButton_Click);

        var filesMenuItem = new ToolStripMenuItem(TeacherClientText.FilesMenu);
        filesMenuItem.DropDownItems.Add(TeacherClientText.RefreshBoth, null, refreshFilesButton_Click);
        filesMenuItem.DropDownItems.Add($"{TeacherClientText.Upload} ->", null, uploadButton_Click);
        filesMenuItem.DropDownItems.Add(TeacherClientText.SendToSelectedStudents, null, sendToSelectedStudentsButton_Click);
        filesMenuItem.DropDownItems.Add(TeacherClientText.SendToAllOnlineStudents, null, sendToAllOnlineStudentsButton_Click);
        filesMenuItem.DropDownItems.Add($"<- {TeacherClientText.Download}", null, downloadButton_Click);
        filesMenuItem.DropDownItems.Add(TeacherClientText.OpenRemote, null, openRemoteButton_Click);
        filesMenuItem.DropDownItems.Add(TeacherClientText.DeleteLocal, null, deleteLocalButton_Click);
        filesMenuItem.DropDownItems.Add(TeacherClientText.DeleteRemote, null, deleteRemoteButton_Click);
        filesMenuItem.DropDownItems.Add(TeacherClientText.NewRemoteFolder, null, newRemoteFolderButton_Click);

        var groupCommandsMenuItem = new ToolStripMenuItem(TeacherClientText.GroupCommandsMenu);
        var destinationFolderMenuItem = new ToolStripMenuItem(TeacherClientText.DestinationFolderMenu);
        destinationFolderMenuItem.DropDownItems.Add(TeacherClientText.ClearDestinationFolderOnSelectedStudents, null, clearSelectedFolderOnSelectedStudentsMenuItem_Click);
        destinationFolderMenuItem.DropDownItems.Add(TeacherClientText.ClearDestinationFolderOnAllOnlineStudents, null, clearSelectedFolderOnAllOnlineStudentsMenuItem_Click);
        groupCommandsMenuItem.DropDownItems.Add(destinationFolderMenuItem);
        var browserMenuItem = new ToolStripMenuItem(TeacherClientText.BrowserCommandsMenu);
        browserMenuItem.DropDownItems.Add(TeacherClientText.LockBrowsersOnAllOnlineStudents, null, lockBrowsersOnAllOnlineStudentsMenuItem_Click);
        groupCommandsMenuItem.DropDownItems.Add(browserMenuItem);
        var inputMenuItem = new ToolStripMenuItem(TeacherClientText.InputCommandsMenu);
        inputMenuItem.DropDownItems.Add(TeacherClientText.LockInputOnAllOnlineStudents, null, lockInputOnAllOnlineStudentsMenuItem_Click);
        inputMenuItem.DropDownItems.Add(TeacherClientText.UnlockInputOnAllOnlineStudents, null, unlockInputOnAllOnlineStudentsMenuItem_Click);
        groupCommandsMenuItem.DropDownItems.Add(inputMenuItem);
        var commandsMenuItem = new ToolStripMenuItem(TeacherClientText.CommandsMenu);
        commandsMenuItem.DropDownItems.Add(TeacherClientText.RunCommandOnSelectedStudents, null, runCommandOnSelectedStudentsMenuItem_Click);
        commandsMenuItem.DropDownItems.Add(TeacherClientText.RunCommandOnAllOnlineStudents, null, runCommandOnAllOnlineStudentsMenuItem_Click);
        groupCommandsMenuItem.DropDownItems.Add(commandsMenuItem);
        var powerMenuItem = new ToolStripMenuItem(TeacherClientText.PowerCommandsMenu);
        var powerSelectedMenuItem = new ToolStripMenuItem(TeacherClientText.SelectedStudentsMenu);
        powerSelectedMenuItem.DropDownItems.Add(TeacherClientText.ShutdownCommand, null, shutdownSelectedStudentsMenuItem_Click);
        powerSelectedMenuItem.DropDownItems.Add(TeacherClientText.RestartCommand, null, restartSelectedStudentsMenuItem_Click);
        powerSelectedMenuItem.DropDownItems.Add(TeacherClientText.LogOffCommand, null, logOffSelectedStudentsMenuItem_Click);
        var powerAllOnlineMenuItem = new ToolStripMenuItem(TeacherClientText.AllOnlineStudentsMenu);
        powerAllOnlineMenuItem.DropDownItems.Add(TeacherClientText.ShutdownCommand, null, shutdownAllOnlineStudentsMenuItem_Click);
        powerAllOnlineMenuItem.DropDownItems.Add(TeacherClientText.RestartCommand, null, restartAllOnlineStudentsMenuItem_Click);
        powerAllOnlineMenuItem.DropDownItems.Add(TeacherClientText.LogOffCommand, null, logOffAllOnlineStudentsMenuItem_Click);
        powerMenuItem.DropDownItems.Add(powerSelectedMenuItem);
        powerMenuItem.DropDownItems.Add(powerAllOnlineMenuItem);
        groupCommandsMenuItem.DropDownItems.Add(powerMenuItem);
        var frequentProgramsMenuItem = new ToolStripMenuItem(TeacherClientText.FrequentProgramsMenu);
        frequentProgramsMenuItem.DropDownItems.Add(TeacherClientText.RefreshFrequentPrograms, null, refreshFrequentProgramsMenuItem_Click);
        frequentProgramsMenuItem.DropDownItems.Add(TeacherClientText.ManageFrequentPrograms, null, manageFrequentProgramsMenuItem_Click);
        groupCommandsMenuItem.DropDownItems.Add(frequentProgramsMenuItem);
        groupCommandsMenuItem.DropDownItems.Add(new ToolStripSeparator());
        var studentWorkMenuItem = new ToolStripMenuItem(TeacherClientText.StudentWorkMenu);
        studentWorkMenuItem.DropDownItems.Add(TeacherClientText.CreateStudentWorkFolderOnAllAgents, null, createStudentWorkFolderOnAllAgentsMenuItem_Click);
        studentWorkMenuItem.DropDownItems.Add(TeacherClientText.CollectStudentWorkToTeacherPc, null, collectStudentWorkToTeacherPcMenuItem_Click);
        studentWorkMenuItem.DropDownItems.Add(TeacherClientText.ClearStudentWorkFolderOnAllAgents, null, clearStudentWorkFolderOnAllAgentsMenuItem_Click);
        groupCommandsMenuItem.DropDownItems.Add(studentWorkMenuItem);

        var helpMenuItem = new ToolStripMenuItem(TeacherClientText.Help);
        helpMenuItem.DropDownItems.Add(TeacherClientText.About, null, aboutMenuItem_Click);

        mainMenuStrip.Dock = DockStyle.Top;
        mainMenuStrip.BackColor = Color.White;
        mainMenuStrip.ImageScalingSize = new Size(20, 20);
        mainMenuStrip.Items.Add(connectionMenuItem);
        mainMenuStrip.Items.Add(processesMenuItem);
        mainMenuStrip.Items.Add(filesMenuItem);
        mainMenuStrip.Items.Add(groupCommandsMenuItem);
        mainMenuStrip.Items.Add(helpMenuItem);

        var quickActionsToolStrip = new ToolStrip
        {
            Dock = DockStyle.Top,
            GripStyle = ToolStripGripStyle.Hidden,
            AutoSize = false,
            Height = 64,
            Padding = new Padding(12, 8, 12, 8),
            BackColor = Color.WhiteSmoke,
            RenderMode = ToolStripRenderMode.System,
            ShowItemToolTips = true,
            ImageScalingSize = new Size(28, 28)
        };

        quickActionsToolStrip.Items.Add(CreateToolbarButton(TeacherClientText.Settings, ToolbarIconKind.Settings, settingsButton_Click));
        quickActionsToolStrip.Items.Add(CreateToolbarButton(TeacherClientText.RefreshAgents, ToolbarIconKind.Refresh, refreshAgentsButton_Click));
        quickActionsToolStrip.Items.Add(CreateToolbarButton(TeacherClientText.ConnectSelectedAgent, ToolbarIconKind.Link, connectSelectedAgentButton_Click));
        quickActionsToolStrip.Items.Add(new ToolStripSeparator());
        quickActionsToolStrip.Items.Add(CreateToolbarButton(TeacherClientText.ProcessesMenu, ToolbarIconKind.Processes, refreshProcessesButton_Click));
        quickActionsToolStrip.Items.Add(CreateToolbarButton(TeacherClientText.FilesMenu, ToolbarIconKind.Folder, refreshFilesButton_Click));

        var topPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 92,
            Padding = new Padding(16, 12, 16, 10),
            BackColor = Color.White
        };

        var headerLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        headerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        settingsButton.Text = TeacherClientText.Settings;
        settingsButton.Dock = DockStyle.Fill;
        settingsButton.Margin = new Padding(0, 0, 16, 0);
        settingsButton.MinimumSize = new Size(130, 48);
        settingsButton.Click += settingsButton_Click;

        statusLabel.Dock = DockStyle.Fill;
        statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        statusLabel.Text = TeacherClientText.StatusReady;
        statusLabel.AutoEllipsis = true;
        statusLabel.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold, GraphicsUnit.Point);

        headerLayout.Controls.Add(settingsButton, 0, 0);
        headerLayout.Controls.Add(statusLabel, 1, 0);
        topPanel.Controls.Add(headerLayout);

        mainTabControl.Dock = DockStyle.Fill;
        mainTabControl.Padding = new Point(18, 6);
        mainTabControl.ItemSize = new Size(140, 34);
        mainTabControl.SizeMode = TabSizeMode.Fixed;
        mainTabControl.TabPages.Add(agentsTabPage);
        mainTabControl.TabPages.Add(processesTabPage);
        mainTabControl.TabPages.Add(filesTabPage);
        mainTabControl.TabPages.Add(registryTabPage);

        agentsTabPage.Text = TeacherClientText.AgentsTab;
        agentsTabPage.BackColor = Color.FromArgb(236, 239, 243);
        processesTabPage.Text = TeacherClientText.ProcessesTab;
        processesTabPage.BackColor = Color.FromArgb(236, 239, 243);
        filesTabPage.Text = TeacherClientText.FilesTab;
        filesTabPage.BackColor = Color.FromArgb(236, 239, 243);
        registryTabPage.Text = TeacherClientText.RegistryTab;
        registryTabPage.BackColor = Color.FromArgb(236, 239, 243);

        ConfigureGrid(agentsGrid);
        ConfigureGrid(processesGrid);
        ConfigureGrid(localFilesGrid);
        ConfigureGrid(remoteFilesGrid);
        agentsGrid.MultiSelect = true;
        agentsGrid.ReadOnly = false;

        agentsGrid.Dock = DockStyle.Fill;
        agentsGrid.CellDoubleClick += agentsGrid_CellDoubleClick;
        agentsGrid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = TeacherClientText.BrowserLock, DataPropertyName = "BrowserLockEnabled", Width = 90 });
        agentsGrid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = TeacherClientText.InputLock, DataPropertyName = "InputLockEnabled", Width = 90 });
        agentsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = TeacherClientText.Source, DataPropertyName = "Source", Width = 100, ReadOnly = true });
        agentsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = TeacherClientText.Status, DataPropertyName = "Status", Width = 100, ReadOnly = true });
        agentsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = TeacherClientText.Group, DataPropertyName = "GroupName", Width = 140, ReadOnly = true });
        agentsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = TeacherClientText.Machine, DataPropertyName = "MachineName", Width = 180, ReadOnly = true });
        agentsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = TeacherClientText.User, DataPropertyName = "CurrentUser", Width = 160, ReadOnly = true });
        agentsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "IP", DataPropertyName = "RespondingAddress", Width = 150, ReadOnly = true });
        agentsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = TeacherClientText.Port, DataPropertyName = "Port", Width = 80, ReadOnly = true });
        agentsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "MAC", DataPropertyName = "MacAddressesDisplay", Width = 220, ReadOnly = true });
        agentsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = TeacherClientText.Notes, DataPropertyName = "Notes", Width = 200, ReadOnly = true });
        agentsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = TeacherClientText.Version.TrimEnd(':'), DataPropertyName = "Version", Width = 110, ReadOnly = true });
        agentsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = TeacherClientText.LastSeenUtc, DataPropertyName = "LastSeenDisplay", Width = 190, ReadOnly = true });

        processesGrid.Dock = DockStyle.Fill;
        processesGrid.CellDoubleClick += processesGrid_CellDoubleClick;
        processesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "PID", DataPropertyName = "Id", Width = 90 });
        processesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = TeacherClientText.Process, DataPropertyName = "Name", Width = 220 });
        processesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = TeacherClientText.Window, DataPropertyName = "MainWindowTitle", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 260 });
        processesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Memory", DataPropertyName = "WorkingSetBytes", Width = 140 });
        processesGrid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = TeacherClientText.Visible, DataPropertyName = "HasVisibleWindow", Width = 90 });
        processesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = TeacherClientText.StartedUtc, DataPropertyName = "StartTimeUtc", Width = 180 });

        localFilesGrid.Dock = DockStyle.Fill;
        localFilesGrid.CellDoubleClick += localFilesGrid_CellDoubleClick;
        localFilesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = TeacherClientText.NameWithIcon, DataPropertyName = "DisplayNameWithIcon", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 58F, MinimumWidth = 260 });
        localFilesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = TeacherClientText.Extension, DataPropertyName = "Extension", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 10F, MinimumWidth = 90 });
        localFilesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = TeacherClientText.Attributes, DataPropertyName = "AttributesDisplay", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 10F, MinimumWidth = 90 });
        localFilesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = TeacherClientText.Size, DataPropertyName = "SizeDisplay", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 10F, MinimumWidth = 95 });
        localFilesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = TeacherClientText.ModifiedUtc, DataPropertyName = "LastModifiedUtc", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 12F, MinimumWidth = 150 });

        remoteFilesGrid.Dock = DockStyle.Fill;
        remoteFilesGrid.CellDoubleClick += remoteFilesGrid_CellDoubleClick;
        remoteFilesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = TeacherClientText.NameWithIcon, DataPropertyName = "DisplayNameWithIcon", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 58F, MinimumWidth = 260 });
        remoteFilesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = TeacherClientText.Extension, DataPropertyName = "Extension", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 10F, MinimumWidth = 90 });
        remoteFilesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = TeacherClientText.Attributes, DataPropertyName = "AttributesDisplay", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 10F, MinimumWidth = 90 });
        remoteFilesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = TeacherClientText.Size, DataPropertyName = "SizeDisplay", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 10F, MinimumWidth = 95 });
        remoteFilesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = TeacherClientText.ModifiedUtc, DataPropertyName = "LastModifiedUtc", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 12F, MinimumWidth = 150 });

        refreshAgentsButton.Text = TeacherClientText.RefreshAgents;
        refreshAgentsButton.Click += refreshAgentsButton_Click;
        connectSelectedAgentButton.Text = TeacherClientText.ConnectSelectedAgent;
        connectSelectedAgentButton.Click += connectSelectedAgentButton_Click;
        addManualAgentButton.Text = TeacherClientText.AddManualAgent;
        addManualAgentButton.Click += addManualAgentButton_Click;
        editManualAgentButton.Text = TeacherClientText.EditManualAgent;
        editManualAgentButton.Click += editManualAgentButton_Click;
        removeManualAgentButton.Text = TeacherClientText.RemoveManualAgent;
        removeManualAgentButton.Click += removeManualAgentButton_Click;

        refreshProcessesButton.Text = TeacherClientText.Refresh;
        refreshProcessesButton.Click += refreshProcessesButton_Click;
        killProcessButton.Text = TeacherClientText.TerminateSelected;
        killProcessButton.Click += killProcessButton_Click;

        refreshFilesButton.Text = TeacherClientText.RefreshBoth;
        refreshFilesButton.Click += refreshFilesButton_Click;
        uploadButton.Text = TeacherClientText.Upload;
        uploadButton.Click += uploadButton_Click;
        downloadButton.Text = TeacherClientText.Download;
        downloadButton.Click += downloadButton_Click;
        openRemoteButton.Text = TeacherClientText.OpenRemote;
        openRemoteButton.Click += openRemoteButton_Click;
        deleteLocalButton.Text = TeacherClientText.DeleteLocal;
        deleteLocalButton.Click += deleteLocalButton_Click;
        deleteRemoteButton.Text = TeacherClientText.DeleteRemote;
        deleteRemoteButton.Click += deleteRemoteButton_Click;
        newRemoteFolderButton.Text = TeacherClientText.NewRemoteFolder;
        newRemoteFolderButton.Click += newRemoteFolderButton_Click;
        upLocalButton.Text = TeacherClientText.Up;
        upLocalButton.Click += upLocalButton_Click;
        upRemoteButton.Text = TeacherClientText.Up;
        upRemoteButton.Click += upRemoteButton_Click;

        var agentsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 3
        };
        agentsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
        agentsLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
        agentsLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var agentsToolStrip = CreateTabToolStrip();
        agentsToolStrip.Items.Add(CreateToolbarButton(TeacherClientText.RefreshAgents, ToolbarIconKind.Refresh, refreshAgentsButton_Click));
        agentsToolStrip.Items.Add(CreateToolbarButton(TeacherClientText.ConnectSelectedAgent, ToolbarIconKind.Link, connectSelectedAgentButton_Click));
        agentsToolStrip.Items.Add(new ToolStripSeparator());
        agentsToolStrip.Items.Add(CreateToolbarButton(TeacherClientText.AddManualAgent, ToolbarIconKind.Add, addManualAgentButton_Click));
        agentsToolStrip.Items.Add(CreateToolbarButton(TeacherClientText.EditManualAgent, ToolbarIconKind.Edit, editManualAgentButton_Click));
        agentsToolStrip.Items.Add(CreateToolbarButton(TeacherClientText.RemoveManualAgent, ToolbarIconKind.Remove, removeManualAgentButton_Click));

        var agentsFilterLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 8,
            RowCount = 1,
            Margin = new Padding(0, 8, 0, 8)
        };
        agentsFilterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110F));
        agentsFilterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320F));
        agentsFilterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96F));
        agentsFilterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200F));
        agentsFilterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96F));
        agentsFilterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160F));
        agentsFilterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280F));
        agentsFilterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        var searchLabel = CreateInlineLabel(TeacherClientText.Search);
        var groupLabel = CreateInlineLabel(TeacherClientText.Group);
        var statusFilterLabel = CreateInlineLabel(TeacherClientText.Status);

        agentSearchTextBox.Dock = DockStyle.Fill;
        agentSearchTextBox.Margin = new Padding(0, 6, 14, 6);
        agentSearchTextBox.MinimumSize = new Size(0, 45);
        agentSearchTextBox.TextChanged += agentFilters_Changed;

        groupFilterComboBox.Dock = DockStyle.Fill;
        groupFilterComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        groupFilterComboBox.Margin = new Padding(0, 6, 14, 6);
        groupFilterComboBox.MinimumSize = new Size(0, 45);
        groupFilterComboBox.SelectedIndexChanged += agentFilters_Changed;

        statusFilterComboBox.Dock = DockStyle.Fill;
        statusFilterComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        statusFilterComboBox.Margin = new Padding(0, 6, 14, 6);
        statusFilterComboBox.MinimumSize = new Size(0, 45);
        statusFilterComboBox.SelectedIndexChanged += agentFilters_Changed;

        autoReconnectCheckBox.Text = TeacherClientText.AutoReconnect;
        autoReconnectCheckBox.Dock = DockStyle.Fill;
        autoReconnectCheckBox.TextAlign = ContentAlignment.MiddleLeft;
        autoReconnectCheckBox.Margin = new Padding(0, 8, 0, 0);
        autoReconnectCheckBox.MinimumSize = new Size(0, 45);

        agentsFilterLayout.Controls.Add(searchLabel, 0, 0);
        agentsFilterLayout.Controls.Add(agentSearchTextBox, 1, 0);
        agentsFilterLayout.Controls.Add(groupLabel, 2, 0);
        agentsFilterLayout.Controls.Add(groupFilterComboBox, 3, 0);
        agentsFilterLayout.Controls.Add(statusFilterLabel, 4, 0);
        agentsFilterLayout.Controls.Add(statusFilterComboBox, 5, 0);
        agentsFilterLayout.Controls.Add(autoReconnectCheckBox, 6, 0);

        agentsLayout.Controls.Add(agentsToolStrip, 0, 0);
        agentsLayout.Controls.Add(agentsFilterLayout, 0, 1);
        agentsLayout.Controls.Add(agentsGrid, 0, 2);
        agentsTabPage.Controls.Add(agentsLayout);

        var processesLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 2
        };
        processesLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
        processesLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var processesToolStrip = CreateTabToolStrip();
        processesToolStrip.Items.Add(CreateToolbarButton(TeacherClientText.Refresh, ToolbarIconKind.Refresh, refreshProcessesButton_Click));
        processesToolStrip.Items.Add(CreateToolbarButton(TeacherClientText.TerminateSelected, ToolbarIconKind.Stop, killProcessButton_Click));

        processesLayout.Controls.Add(processesToolStrip, 0, 0);
        processesLayout.Controls.Add(processesGrid, 0, 1);
        processesTabPage.Controls.Add(processesLayout);

        var filesLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 2
        };
        filesLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
        filesLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var filesToolStrip = CreateTabToolStrip();
        filesToolStrip.Items.Add(CreateToolbarButton(TeacherClientText.RefreshBoth, ToolbarIconKind.Refresh, refreshFilesButton_Click));
        filesToolStrip.Items.Add(new ToolStripSeparator());
        filesToolStrip.Items.Add(CreateToolbarButton(TeacherClientText.Upload, ToolbarIconKind.Upload, uploadButton_Click));
        filesToolStrip.Items.Add(CreateToolbarButton(TeacherClientText.SendToSelectedStudents, ToolbarIconKind.UploadGroup, sendToSelectedStudentsButton_Click));
        filesToolStrip.Items.Add(CreateToolbarButton(TeacherClientText.SendToAllOnlineStudents, ToolbarIconKind.Broadcast, sendToAllOnlineStudentsButton_Click));
        filesToolStrip.Items.Add(CreateToolbarButton(TeacherClientText.Download, ToolbarIconKind.Download, downloadButton_Click));
        filesToolStrip.Items.Add(CreateToolbarButton(TeacherClientText.OpenRemote, ToolbarIconKind.OpenRemote, openRemoteButton_Click, showText: true));
        filesToolStrip.Items.Add(new ToolStripSeparator());
        filesToolStrip.Items.Add(CreateToolbarButton(TeacherClientText.DeleteLocal, ToolbarIconKind.Remove, deleteLocalButton_Click));
        filesToolStrip.Items.Add(CreateToolbarButton(TeacherClientText.DeleteRemote, ToolbarIconKind.Remove, deleteRemoteButton_Click));
        filesToolStrip.Items.Add(CreateToolbarButton(TeacherClientText.NewRemoteFolder, ToolbarIconKind.NewFolder, newRemoteFolderButton_Click));

        var filesPanelsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.FromArgb(236, 239, 243)
        };
        filesPanelsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        filesPanelsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));

        var localPanelLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(0, 6, 8, 0)
        };
        localPanelLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
        localPanelLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
        localPanelLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var localLabel = new Label
        {
            Text = TeacherClientText.TeacherPc,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold, GraphicsUnit.Point),
            TextAlign = ContentAlignment.MiddleLeft
        };

        var localPathLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1
        };
        localPathLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140F));
        localPathLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76F));
        localPathLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        localDriveComboBox.Dock = DockStyle.Fill;
        localDriveComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        localDriveComboBox.Margin = new Padding(0, 0, 10, 0);
        localDriveComboBox.MinimumSize = new Size(0, 42);
        localDriveComboBox.SelectedIndexChanged += localDriveComboBox_SelectedIndexChanged;

        upLocalButton.Dock = DockStyle.Fill;
        upLocalButton.MinimumSize = new Size(64, 42);
        upLocalButton.Margin = new Padding(0, 0, 10, 0);

        localPathTextBox.Dock = DockStyle.Fill;
        localPathTextBox.MinimumSize = new Size(0, 42);
        localPathTextBox.ReadOnly = true;
        localPathTextBox.Margin = new Padding(0, 2, 0, 2);

        localPathLayout.Controls.Add(localDriveComboBox, 0, 0);
        localPathLayout.Controls.Add(upLocalButton, 1, 0);
        localPathLayout.Controls.Add(localPathTextBox, 2, 0);

        localPanelLayout.Controls.Add(localLabel, 0, 0);
        localPanelLayout.Controls.Add(localPathLayout, 0, 1);
        localPanelLayout.Controls.Add(localFilesGrid, 0, 2);

        var remotePanelLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(8, 6, 0, 0)
        };
        remotePanelLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
        remotePanelLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
        remotePanelLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var remoteLabel = new Label
        {
            Text = TeacherClientText.StudentPc,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold, GraphicsUnit.Point),
            TextAlign = ContentAlignment.MiddleLeft
        };

        var remotePathLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1
        };
        remotePathLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140F));
        remotePathLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76F));
        remotePathLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        remoteDriveComboBox.Dock = DockStyle.Fill;
        remoteDriveComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        remoteDriveComboBox.Margin = new Padding(0, 0, 10, 0);
        remoteDriveComboBox.MinimumSize = new Size(0, 42);
        remoteDriveComboBox.SelectedIndexChanged += remoteDriveComboBox_SelectedIndexChanged;

        upRemoteButton.Dock = DockStyle.Fill;
        upRemoteButton.MinimumSize = new Size(64, 42);
        upRemoteButton.Margin = new Padding(0, 0, 10, 0);

        remotePathTextBox.Dock = DockStyle.Fill;
        remotePathTextBox.MinimumSize = new Size(0, 42);
        remotePathTextBox.ReadOnly = true;
        remotePathTextBox.Margin = new Padding(0, 2, 0, 2);

        remotePathLayout.Controls.Add(remoteDriveComboBox, 0, 0);
        remotePathLayout.Controls.Add(upRemoteButton, 1, 0);
        remotePathLayout.Controls.Add(remotePathTextBox, 2, 0);

        remotePanelLayout.Controls.Add(remoteLabel, 0, 0);
        remotePanelLayout.Controls.Add(remotePathLayout, 0, 1);
        remotePanelLayout.Controls.Add(remoteFilesGrid, 0, 2);

        filesPanelsLayout.Controls.Add(localPanelLayout, 0, 0);
        filesPanelsLayout.Controls.Add(remotePanelLayout, 1, 0);

        filesLayout.Controls.Add(filesToolStrip, 0, 0);
        filesLayout.Controls.Add(filesPanelsLayout, 0, 1);
        filesTabPage.Controls.Add(filesLayout);

        ConfigureGrid(registryValuesGrid);
        registryValuesGrid.Dock = DockStyle.Fill;
        registryValuesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = TeacherClientText.Name, DataPropertyName = "Name", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 35F, MinimumWidth = 180 });
        registryValuesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = TeacherClientText.RegistryValueType, DataPropertyName = "TypeDisplay", Width = 140 });
        registryValuesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = TeacherClientText.RegistryValueData, DataPropertyName = "DataDisplay", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 65F, MinimumWidth = 260 });

        registryTreeView.Dock = DockStyle.Fill;
        registryTreeView.BackColor = Color.White;
        registryTreeView.BorderStyle = BorderStyle.None;
        registryTreeView.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        registryTreeView.BeforeExpand += registryTreeView_BeforeExpand;
        registryTreeView.AfterSelect += registryTreeView_AfterSelect;

        var registryLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 2
        };
        registryLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
        registryLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var registryToolStrip = CreateTabToolStrip();
        registryToolStrip.Items.Add(CreateToolbarButton(TeacherClientText.Refresh, ToolbarIconKind.Refresh, refreshRegistryButton_Click));

        var registrySplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 340,
            Panel1MinSize = 200,
            Panel2MinSize = 200
        };
        registrySplit.Panel1.Controls.Add(registryTreeView);
        registrySplit.Panel2.Controls.Add(registryValuesGrid);

        registryLayout.Controls.Add(registryToolStrip, 0, 0);
        registryLayout.Controls.Add(registrySplit, 0, 1);
        registryTabPage.Controls.Add(registryLayout);

        Controls.Add(mainTabControl);
        Controls.Add(topPanel);
        Controls.Add(quickActionsToolStrip);
        Controls.Add(mainMenuStrip);
        ResumeLayout(false);
        PerformLayout();
    }

    private static ToolStrip CreateTabToolStrip()
    {
        return new ToolStrip
        {
            Dock = DockStyle.Fill,
            GripStyle = ToolStripGripStyle.Hidden,
            AutoSize = false,
            Height = 58,
            Padding = new Padding(4),
            BackColor = Color.White,
            RenderMode = ToolStripRenderMode.System,
            ShowItemToolTips = true,
            ImageScalingSize = new Size(28, 28)
        };
    }

    private static ToolStripButton CreateToolbarButton(string toolTipText, ToolbarIconKind iconKind, EventHandler onClick, bool showText = false)
    {
        var button = new ToolStripButton
        {
            DisplayStyle = showText ? ToolStripItemDisplayStyle.ImageAndText : ToolStripItemDisplayStyle.Image,
            AutoSize = false,
            Width = showText ? 170 : 46,
            Height = 46,
            Image = CreateToolbarIcon(iconKind),
            Text = showText ? toolTipText : string.Empty,
            ToolTipText = toolTipText,
            Margin = new Padding(2),
            ImageTransparentColor = Color.Magenta,
            TextImageRelation = TextImageRelation.ImageBeforeText
        };
        button.Click += onClick;
        return button;
    }

    private static Bitmap CreateToolbarIcon(ToolbarIconKind iconKind)
    {
        var bitmap = new Bitmap(28, 28);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);

        var accent = iconKind switch
        {
            ToolbarIconKind.Settings => Color.FromArgb(79, 70, 229),
            ToolbarIconKind.Refresh => Color.FromArgb(8, 145, 178),
            ToolbarIconKind.Link => Color.FromArgb(22, 163, 74),
            ToolbarIconKind.Add => Color.FromArgb(34, 197, 94),
            ToolbarIconKind.Edit => Color.FromArgb(245, 158, 11),
            ToolbarIconKind.Remove => Color.FromArgb(220, 38, 38),
            ToolbarIconKind.Processes => Color.FromArgb(124, 58, 237),
            ToolbarIconKind.Stop => Color.FromArgb(190, 24, 93),
            ToolbarIconKind.Folder => Color.FromArgb(217, 119, 6),
            ToolbarIconKind.Upload => Color.FromArgb(2, 132, 199),
            ToolbarIconKind.UploadGroup => Color.FromArgb(37, 99, 235),
            ToolbarIconKind.Download => Color.FromArgb(14, 116, 144),
            ToolbarIconKind.OpenRemote => Color.FromArgb(37, 99, 235),
            ToolbarIconKind.Broadcast => Color.FromArgb(124, 58, 237),
            ToolbarIconKind.NewFolder => Color.FromArgb(202, 138, 4),
            _ => Color.FromArgb(71, 85, 105)
        };

        using var pen = new Pen(accent, 2.8F)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };

        using var fillBrush = new SolidBrush(Color.FromArgb(32, accent));
        using var accentBrush = new SolidBrush(accent);

        graphics.FillEllipse(fillBrush, 1, 1, 26, 26);

        switch (iconKind)
        {
            case ToolbarIconKind.Settings:
                graphics.DrawEllipse(pen, 8, 8, 12, 12);
                graphics.FillEllipse(accentBrush, 12, 12, 4, 4);
                graphics.DrawLine(pen, 14, 4, 14, 8);
                graphics.DrawLine(pen, 14, 20, 14, 24);
                graphics.DrawLine(pen, 4, 14, 8, 14);
                graphics.DrawLine(pen, 20, 14, 24, 14);
                break;
            case ToolbarIconKind.Refresh:
                graphics.DrawArc(pen, 6, 6, 16, 16, 35, 250);
                graphics.DrawLine(pen, 19, 6, 23, 6);
                graphics.DrawLine(pen, 23, 6, 23, 10);
                break;
            case ToolbarIconKind.Link:
                graphics.DrawArc(pen, 4, 9, 10, 8, 300, 220);
                graphics.DrawArc(pen, 14, 9, 10, 8, 120, 220);
                graphics.DrawLine(pen, 10, 14, 18, 14);
                break;
            case ToolbarIconKind.Add:
                graphics.DrawLine(pen, 14, 6, 14, 22);
                graphics.DrawLine(pen, 6, 14, 22, 14);
                break;
            case ToolbarIconKind.Edit:
                graphics.DrawLine(pen, 7, 20, 18, 9);
                graphics.DrawLine(pen, 17, 8, 21, 12);
                graphics.DrawLine(pen, 7, 20, 6, 23);
                break;
            case ToolbarIconKind.Remove:
                graphics.DrawLine(pen, 8, 8, 20, 20);
                graphics.DrawLine(pen, 20, 8, 8, 20);
                break;
            case ToolbarIconKind.Processes:
                graphics.DrawRectangle(pen, 5, 8, 5, 12);
                graphics.DrawRectangle(pen, 12, 10, 5, 10);
                graphics.DrawRectangle(pen, 19, 6, 4, 14);
                break;
            case ToolbarIconKind.Stop:
                graphics.FillRectangle(accentBrush, 7, 7, 14, 14);
                break;
            case ToolbarIconKind.Folder:
                graphics.DrawRectangle(pen, 5, 10, 18, 11);
                graphics.DrawLine(pen, 5, 10, 9, 6);
                graphics.DrawLine(pen, 9, 6, 15, 6);
                graphics.DrawLine(pen, 15, 6, 17, 10);
                break;
            case ToolbarIconKind.Upload:
                graphics.DrawLine(pen, 14, 22, 14, 6);
                graphics.DrawLine(pen, 10, 10, 14, 6);
                graphics.DrawLine(pen, 18, 10, 14, 6);
                graphics.DrawLine(pen, 7, 22, 21, 22);
                break;
            case ToolbarIconKind.UploadGroup:
                graphics.DrawLine(pen, 10, 18, 10, 7);
                graphics.DrawLine(pen, 7, 10, 10, 7);
                graphics.DrawLine(pen, 13, 10, 10, 7);
                graphics.DrawEllipse(pen, 4, 18, 5, 5);
                graphics.DrawEllipse(pen, 12, 18, 5, 5);
                graphics.DrawEllipse(pen, 20, 18, 5, 5);
                break;
            case ToolbarIconKind.Download:
                graphics.DrawLine(pen, 14, 6, 14, 22);
                graphics.DrawLine(pen, 10, 18, 14, 22);
                graphics.DrawLine(pen, 18, 18, 14, 22);
                graphics.DrawLine(pen, 7, 6, 21, 6);
                break;
            case ToolbarIconKind.OpenRemote:
                graphics.DrawRectangle(pen, 5, 7, 10, 14);
                graphics.DrawLine(pen, 12, 14, 22, 14);
                graphics.DrawLine(pen, 18, 10, 22, 14);
                graphics.DrawLine(pen, 18, 18, 22, 14);
                break;
            case ToolbarIconKind.Broadcast:
                graphics.FillEllipse(accentBrush, 12, 12, 4, 4);
                graphics.DrawArc(pen, 8, 8, 12, 12, 315, 90);
                graphics.DrawArc(pen, 5, 5, 18, 18, 315, 90);
                graphics.DrawArc(pen, 2, 2, 24, 24, 315, 90);
                break;
            case ToolbarIconKind.NewFolder:
                graphics.DrawRectangle(pen, 4, 11, 14, 10);
                graphics.DrawLine(pen, 4, 11, 8, 7);
                graphics.DrawLine(pen, 8, 7, 13, 7);
                graphics.DrawLine(pen, 13, 7, 15, 11);
                graphics.DrawLine(pen, 21, 9, 21, 21);
                graphics.DrawLine(pen, 15, 15, 27, 15);
                break;
        }

        return bitmap;
    }

    private enum ToolbarIconKind
    {
        Settings,
        Refresh,
        Link,
        Add,
        Edit,
        Remove,
        Processes,
        Stop,
        Folder,
        Upload,
        UploadGroup,
        Download,
        OpenRemote,
        Broadcast,
        NewFolder
    }

    private static Label CreateInlineLabel(string text)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0),
            AutoSize = false
        };
    }

    private static void ConfigureGrid(DataGridView grid)
    {
        grid.AutoGenerateColumns = false;
        grid.BackgroundColor = Color.White;
        grid.BorderStyle = BorderStyle.FixedSingle;
        grid.MultiSelect = false;
        grid.ReadOnly = true;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.ColumnHeadersHeight = 42;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.RowTemplate.Height = 34;
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(241, 245, 249);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(15, 23, 42);
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(30, 120, 210);
        grid.DefaultCellStyle.SelectionForeColor = Color.White;
        grid.GridColor = Color.FromArgb(216, 221, 227);
    }
}
