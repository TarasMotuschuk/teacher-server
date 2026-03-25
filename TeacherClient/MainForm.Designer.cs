#nullable enable

using System.Drawing.Drawing2D;

namespace TeacherClient;

partial class MainForm
{
    private System.ComponentModel.IContainer? components = null;
    private MenuStrip mainMenuStrip = null!;
    private TabControl mainTabControl = null!;
    private TabPage agentsTabPage = null!;
    private TabPage processesTabPage = null!;
    private TabPage filesTabPage = null!;
    private Button settingsButton = null!;
    private Label statusLabel = null!;
    private DataGridView processesGrid = null!;
    private Button refreshProcessesButton = null!;
    private Button killProcessButton = null!;
    private DataGridView localFilesGrid = null!;
    private DataGridView remoteFilesGrid = null!;
    private TextBox localPathTextBox = null!;
    private TextBox remotePathTextBox = null!;
    private Button refreshFilesButton = null!;
    private Button uploadButton = null!;
    private Button downloadButton = null!;
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
        settingsButton = new Button();
        statusLabel = new Label();
        processesGrid = new DataGridView();
        refreshProcessesButton = new Button();
        killProcessButton = new Button();
        localFilesGrid = new DataGridView();
        remoteFilesGrid = new DataGridView();
        localPathTextBox = new TextBox();
        remotePathTextBox = new TextBox();
        refreshFilesButton = new Button();
        uploadButton = new Button();
        downloadButton = new Button();
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

        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.FromArgb(236, 239, 243);
        Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        Text = "Teacher Classroom Client";
        MinimumSize = new Size(1280, 840);
        WindowState = FormWindowState.Maximized;
        MainMenuStrip = mainMenuStrip;

        var connectionMenuItem = new ToolStripMenuItem("Connection");
        connectionMenuItem.DropDownItems.Add("Settings", null, settingsButton_Click);
        connectionMenuItem.DropDownItems.Add("Refresh Agents", null, refreshAgentsButton_Click);
        connectionMenuItem.DropDownItems.Add("Connect Selected Agent", null, connectSelectedAgentButton_Click);
        connectionMenuItem.DropDownItems.Add("Add Manual Agent", null, addManualAgentButton_Click);
        connectionMenuItem.DropDownItems.Add("Edit Manual Agent", null, editManualAgentButton_Click);
        connectionMenuItem.DropDownItems.Add("Remove Manual Agent", null, removeManualAgentButton_Click);

        var processesMenuItem = new ToolStripMenuItem("Processes");
        processesMenuItem.DropDownItems.Add("Refresh", null, refreshProcessesButton_Click);
        processesMenuItem.DropDownItems.Add("Terminate Selected", null, killProcessButton_Click);

        var filesMenuItem = new ToolStripMenuItem("Files");
        filesMenuItem.DropDownItems.Add("Refresh Both", null, refreshFilesButton_Click);
        filesMenuItem.DropDownItems.Add("Upload ->", null, uploadButton_Click);
        filesMenuItem.DropDownItems.Add("<- Download", null, downloadButton_Click);
        filesMenuItem.DropDownItems.Add("Delete Local", null, deleteLocalButton_Click);
        filesMenuItem.DropDownItems.Add("Delete Remote", null, deleteRemoteButton_Click);
        filesMenuItem.DropDownItems.Add("New Remote Folder", null, newRemoteFolderButton_Click);

        var helpMenuItem = new ToolStripMenuItem("Help");
        helpMenuItem.DropDownItems.Add("About", null, aboutMenuItem_Click);

        mainMenuStrip.Dock = DockStyle.Top;
        mainMenuStrip.BackColor = Color.White;
        mainMenuStrip.ImageScalingSize = new Size(20, 20);
        mainMenuStrip.Items.Add(connectionMenuItem);
        mainMenuStrip.Items.Add(processesMenuItem);
        mainMenuStrip.Items.Add(filesMenuItem);
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

        quickActionsToolStrip.Items.Add(CreateToolbarButton("Settings", ToolbarIconKind.Settings, settingsButton_Click));
        quickActionsToolStrip.Items.Add(CreateToolbarButton("Refresh Agents", ToolbarIconKind.Refresh, refreshAgentsButton_Click));
        quickActionsToolStrip.Items.Add(CreateToolbarButton("Connect Selected", ToolbarIconKind.Link, connectSelectedAgentButton_Click));
        quickActionsToolStrip.Items.Add(new ToolStripSeparator());
        quickActionsToolStrip.Items.Add(CreateToolbarButton("Refresh Processes", ToolbarIconKind.Processes, refreshProcessesButton_Click));
        quickActionsToolStrip.Items.Add(CreateToolbarButton("Refresh Files", ToolbarIconKind.Folder, refreshFilesButton_Click));

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

        settingsButton.Text = "Settings";
        settingsButton.Dock = DockStyle.Fill;
        settingsButton.Margin = new Padding(0, 0, 16, 0);
        settingsButton.MinimumSize = new Size(130, 48);
        settingsButton.Click += settingsButton_Click;

        statusLabel.Dock = DockStyle.Fill;
        statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        statusLabel.Text = "Ready. Use the Agents tab to select a student machine, then connect.";
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

        agentsTabPage.Text = "Agents";
        agentsTabPage.BackColor = Color.FromArgb(236, 239, 243);
        processesTabPage.Text = "Processes";
        processesTabPage.BackColor = Color.FromArgb(236, 239, 243);
        filesTabPage.Text = "Files";
        filesTabPage.BackColor = Color.FromArgb(236, 239, 243);

        ConfigureGrid(agentsGrid);
        ConfigureGrid(processesGrid);
        ConfigureGrid(localFilesGrid);
        ConfigureGrid(remoteFilesGrid);

        agentsGrid.Dock = DockStyle.Fill;
        agentsGrid.CellDoubleClick += agentsGrid_CellDoubleClick;
        agentsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Source", DataPropertyName = "Source", Width = 100 });
        agentsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Status", DataPropertyName = "Status", Width = 100 });
        agentsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Group", DataPropertyName = "GroupName", Width = 140 });
        agentsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Machine", DataPropertyName = "MachineName", Width = 180 });
        agentsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "User", DataPropertyName = "CurrentUser", Width = 160 });
        agentsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "IP", DataPropertyName = "RespondingAddress", Width = 150 });
        agentsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Port", DataPropertyName = "Port", Width = 80 });
        agentsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "MACs", DataPropertyName = "MacAddressesDisplay", Width = 220 });
        agentsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Notes", DataPropertyName = "Notes", Width = 200 });
        agentsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Version", DataPropertyName = "Version", Width = 110 });
        agentsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Last Seen UTC", DataPropertyName = "LastSeenDisplay", Width = 190 });

        processesGrid.Dock = DockStyle.Fill;
        processesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "PID", DataPropertyName = "Id", Width = 90 });
        processesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Process", DataPropertyName = "Name", Width = 220 });
        processesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Window", DataPropertyName = "MainWindowTitle", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 260 });
        processesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Memory", DataPropertyName = "WorkingSetBytes", Width = 140 });
        processesGrid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Visible", DataPropertyName = "HasVisibleWindow", Width = 90 });
        processesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Started UTC", DataPropertyName = "StartTimeUtc", Width = 180 });

        localFilesGrid.Dock = DockStyle.Fill;
        localFilesGrid.CellDoubleClick += localFilesGrid_CellDoubleClick;
        localFilesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Name", DataPropertyName = "Name", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 220 });
        localFilesGrid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Dir", DataPropertyName = "IsDirectory", Width = 60 });
        localFilesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Size", DataPropertyName = "Size", Width = 110 });
        localFilesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Modified UTC", DataPropertyName = "LastModifiedUtc", Width = 190 });

        remoteFilesGrid.Dock = DockStyle.Fill;
        remoteFilesGrid.CellDoubleClick += remoteFilesGrid_CellDoubleClick;
        remoteFilesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Name", DataPropertyName = "Name", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, MinimumWidth = 220 });
        remoteFilesGrid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Dir", DataPropertyName = "IsDirectory", Width = 60 });
        remoteFilesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Size", DataPropertyName = "Size", Width = 110 });
        remoteFilesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Modified UTC", DataPropertyName = "LastModifiedUtc", Width = 190 });

        refreshAgentsButton.Text = "Refresh Agents";
        refreshAgentsButton.Click += refreshAgentsButton_Click;
        connectSelectedAgentButton.Text = "Connect Selected";
        connectSelectedAgentButton.Click += connectSelectedAgentButton_Click;
        addManualAgentButton.Text = "Add Manual";
        addManualAgentButton.Click += addManualAgentButton_Click;
        editManualAgentButton.Text = "Edit Manual";
        editManualAgentButton.Click += editManualAgentButton_Click;
        removeManualAgentButton.Text = "Remove Manual";
        removeManualAgentButton.Click += removeManualAgentButton_Click;

        refreshProcessesButton.Text = "Refresh";
        refreshProcessesButton.Click += refreshProcessesButton_Click;
        killProcessButton.Text = "Terminate Selected";
        killProcessButton.Click += killProcessButton_Click;

        refreshFilesButton.Text = "Refresh Both";
        refreshFilesButton.Click += refreshFilesButton_Click;
        uploadButton.Text = "Upload";
        uploadButton.Click += uploadButton_Click;
        downloadButton.Text = "Download";
        downloadButton.Click += downloadButton_Click;
        deleteLocalButton.Text = "Delete Local";
        deleteLocalButton.Click += deleteLocalButton_Click;
        deleteRemoteButton.Text = "Delete Remote";
        deleteRemoteButton.Click += deleteRemoteButton_Click;
        newRemoteFolderButton.Text = "New Folder";
        newRemoteFolderButton.Click += newRemoteFolderButton_Click;
        upLocalButton.Text = "Up";
        upLocalButton.Click += upLocalButton_Click;
        upRemoteButton.Text = "Up";
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
        agentsToolStrip.Items.Add(CreateToolbarButton("Refresh Agents", ToolbarIconKind.Refresh, refreshAgentsButton_Click));
        agentsToolStrip.Items.Add(CreateToolbarButton("Connect Selected", ToolbarIconKind.Link, connectSelectedAgentButton_Click));
        agentsToolStrip.Items.Add(new ToolStripSeparator());
        agentsToolStrip.Items.Add(CreateToolbarButton("Add Manual", ToolbarIconKind.Add, addManualAgentButton_Click));
        agentsToolStrip.Items.Add(CreateToolbarButton("Edit Manual", ToolbarIconKind.Edit, editManualAgentButton_Click));
        agentsToolStrip.Items.Add(CreateToolbarButton("Remove Manual", ToolbarIconKind.Remove, removeManualAgentButton_Click));

        var agentsFilterLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 8,
            RowCount = 1,
            Margin = new Padding(0, 8, 0, 8)
        };
        agentsFilterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 62F));
        agentsFilterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260F));
        agentsFilterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 56F));
        agentsFilterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170F));
        agentsFilterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60F));
        agentsFilterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140F));
        agentsFilterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170F));
        agentsFilterLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        var searchLabel = CreateInlineLabel("Search");
        var groupLabel = CreateInlineLabel("Group");
        var statusFilterLabel = CreateInlineLabel("Status");

        agentSearchTextBox.Dock = DockStyle.Fill;
        agentSearchTextBox.Margin = new Padding(0, 6, 14, 6);
        agentSearchTextBox.MinimumSize = new Size(0, 40);
        agentSearchTextBox.TextChanged += agentFilters_Changed;

        groupFilterComboBox.Dock = DockStyle.Fill;
        groupFilterComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        groupFilterComboBox.Margin = new Padding(0, 6, 14, 6);
        groupFilterComboBox.SelectedIndexChanged += agentFilters_Changed;

        statusFilterComboBox.Dock = DockStyle.Fill;
        statusFilterComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        statusFilterComboBox.Margin = new Padding(0, 6, 14, 6);
        statusFilterComboBox.SelectedIndexChanged += agentFilters_Changed;

        autoReconnectCheckBox.Text = "Auto-reconnect";
        autoReconnectCheckBox.Dock = DockStyle.Fill;
        autoReconnectCheckBox.TextAlign = ContentAlignment.MiddleLeft;
        autoReconnectCheckBox.Margin = new Padding(0, 8, 0, 0);

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
        processesToolStrip.Items.Add(CreateToolbarButton("Refresh", ToolbarIconKind.Refresh, refreshProcessesButton_Click));
        processesToolStrip.Items.Add(CreateToolbarButton("Terminate Selected", ToolbarIconKind.Stop, killProcessButton_Click));

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
        filesToolStrip.Items.Add(CreateToolbarButton("Refresh Both", ToolbarIconKind.Refresh, refreshFilesButton_Click));
        filesToolStrip.Items.Add(new ToolStripSeparator());
        filesToolStrip.Items.Add(CreateToolbarButton("Upload", ToolbarIconKind.Upload, uploadButton_Click));
        filesToolStrip.Items.Add(CreateToolbarButton("Download", ToolbarIconKind.Download, downloadButton_Click));
        filesToolStrip.Items.Add(new ToolStripSeparator());
        filesToolStrip.Items.Add(CreateToolbarButton("Delete Local", ToolbarIconKind.Remove, deleteLocalButton_Click));
        filesToolStrip.Items.Add(CreateToolbarButton("Delete Remote", ToolbarIconKind.Remove, deleteRemoteButton_Click));
        filesToolStrip.Items.Add(CreateToolbarButton("New Folder", ToolbarIconKind.NewFolder, newRemoteFolderButton_Click));

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
            Text = "Teacher PC",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold, GraphicsUnit.Point),
            TextAlign = ContentAlignment.MiddleLeft
        };

        var localPathLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        localPathLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76F));
        localPathLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        upLocalButton.Dock = DockStyle.Fill;
        upLocalButton.MinimumSize = new Size(64, 42);
        upLocalButton.Margin = new Padding(0, 0, 10, 0);

        localPathTextBox.Dock = DockStyle.Fill;
        localPathTextBox.MinimumSize = new Size(0, 42);
        localPathTextBox.ReadOnly = true;
        localPathTextBox.Margin = new Padding(0, 2, 0, 2);

        localPathLayout.Controls.Add(upLocalButton, 0, 0);
        localPathLayout.Controls.Add(localPathTextBox, 1, 0);

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
            Text = "Student PC",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold, GraphicsUnit.Point),
            TextAlign = ContentAlignment.MiddleLeft
        };

        var remotePathLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        remotePathLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76F));
        remotePathLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        upRemoteButton.Dock = DockStyle.Fill;
        upRemoteButton.MinimumSize = new Size(64, 42);
        upRemoteButton.Margin = new Padding(0, 0, 10, 0);

        remotePathTextBox.Dock = DockStyle.Fill;
        remotePathTextBox.MinimumSize = new Size(0, 42);
        remotePathTextBox.ReadOnly = true;
        remotePathTextBox.Margin = new Padding(0, 2, 0, 2);

        remotePathLayout.Controls.Add(upRemoteButton, 0, 0);
        remotePathLayout.Controls.Add(remotePathTextBox, 1, 0);

        remotePanelLayout.Controls.Add(remoteLabel, 0, 0);
        remotePanelLayout.Controls.Add(remotePathLayout, 0, 1);
        remotePanelLayout.Controls.Add(remoteFilesGrid, 0, 2);

        filesPanelsLayout.Controls.Add(localPanelLayout, 0, 0);
        filesPanelsLayout.Controls.Add(remotePanelLayout, 1, 0);

        filesLayout.Controls.Add(filesToolStrip, 0, 0);
        filesLayout.Controls.Add(filesPanelsLayout, 0, 1);
        filesTabPage.Controls.Add(filesLayout);

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

    private static ToolStripButton CreateToolbarButton(string toolTipText, ToolbarIconKind iconKind, EventHandler onClick)
    {
        var button = new ToolStripButton
        {
            DisplayStyle = ToolStripItemDisplayStyle.Image,
            AutoSize = false,
            Width = 46,
            Height = 46,
            Image = CreateToolbarIcon(iconKind),
            ToolTipText = toolTipText,
            Margin = new Padding(2),
            ImageTransparentColor = Color.Magenta
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
            ToolbarIconKind.Download => Color.FromArgb(14, 116, 144),
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
            case ToolbarIconKind.Download:
                graphics.DrawLine(pen, 14, 6, 14, 22);
                graphics.DrawLine(pen, 10, 18, 14, 22);
                graphics.DrawLine(pen, 18, 18, 14, 22);
                graphics.DrawLine(pen, 7, 6, 21, 6);
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
        Download,
        NewFolder
    }

    private static Label CreateInlineLabel(string text)
    {
        return new Label
        {
            Text = text,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Margin = new Padding(0)
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
