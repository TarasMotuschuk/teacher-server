#nullable enable

namespace TeacherClient;

partial class MainForm
{
    private System.ComponentModel.IContainer? components = null;
    private MenuStrip mainMenuStrip = null!;
    private TabControl mainTabControl = null!;
    private TabPage agentsTabPage = null!;
    private TabPage processesTabPage = null!;
    private TabPage filesTabPage = null!;
    private TextBox serverUrlTextBox = null!;
    private TextBox sharedSecretTextBox = null!;
    private Button connectButton = null!;
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
        serverUrlTextBox = new TextBox();
        sharedSecretTextBox = new TextBox();
        connectButton = new Button();
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
        connectionMenuItem.DropDownItems.Add("Connect", null, connectButton_Click);
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
            Height = 42,
            Padding = new Padding(12, 6, 12, 6),
            BackColor = Color.WhiteSmoke,
            RenderMode = ToolStripRenderMode.System
        };

        quickActionsToolStrip.Items.Add(CreateToolbarButton("Connect", connectButton_Click, 90));
        quickActionsToolStrip.Items.Add(CreateToolbarButton("Refresh Agents", refreshAgentsButton_Click, 120));
        quickActionsToolStrip.Items.Add(CreateToolbarButton("Connect Selected", connectSelectedAgentButton_Click, 125));
        quickActionsToolStrip.Items.Add(new ToolStripSeparator());
        quickActionsToolStrip.Items.Add(CreateToolbarButton("Refresh Processes", refreshProcessesButton_Click, 130));
        quickActionsToolStrip.Items.Add(CreateToolbarButton("Refresh Files", refreshFilesButton_Click, 110));

        var topPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 112,
            Padding = new Padding(16, 14, 16, 12),
            BackColor = Color.White
        };

        var headerLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 1
        };
        headerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90F));
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 360F));
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70F));
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260F));
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130F));
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        var serverLabel = new Label
        {
            Text = "Server URL",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoSize = false,
            Margin = new Padding(0, 0, 10, 0)
        };

        serverUrlTextBox.Dock = DockStyle.Fill;
        serverUrlTextBox.Margin = new Padding(0, 4, 14, 4);
        serverUrlTextBox.MinimumSize = new Size(0, 42);

        var secretLabel = new Label
        {
            Text = "Secret",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoSize = false,
            Margin = new Padding(0, 0, 10, 0)
        };

        sharedSecretTextBox.Dock = DockStyle.Fill;
        sharedSecretTextBox.Margin = new Padding(0, 4, 14, 4);
        sharedSecretTextBox.MinimumSize = new Size(0, 42);

        connectButton.Text = "Connect";
        connectButton.Dock = DockStyle.Fill;
        connectButton.Margin = new Padding(0, 0, 0, 0);
        connectButton.MinimumSize = new Size(120, 48);
        connectButton.Click += connectButton_Click;

        statusLabel.Dock = DockStyle.Fill;
        statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        statusLabel.Text = "Ready";
        statusLabel.AutoEllipsis = true;
        statusLabel.Font = new Font("Segoe UI", 10.5F, FontStyle.SemiBold, GraphicsUnit.Point);

        headerLayout.Controls.Add(serverLabel, 0, 0);
        headerLayout.Controls.Add(serverUrlTextBox, 1, 0);
        headerLayout.Controls.Add(secretLabel, 2, 0);
        headerLayout.Controls.Add(sharedSecretTextBox, 3, 0);
        headerLayout.Controls.Add(connectButton, 4, 0);
        headerLayout.Controls.Add(statusLabel, 5, 0);
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
        agentsToolStrip.Items.Add(CreateToolbarButton("Refresh Agents", refreshAgentsButton_Click, 120));
        agentsToolStrip.Items.Add(CreateToolbarButton("Connect Selected", connectSelectedAgentButton_Click, 130));
        agentsToolStrip.Items.Add(new ToolStripSeparator());
        agentsToolStrip.Items.Add(CreateToolbarButton("Add Manual", addManualAgentButton_Click, 100));
        agentsToolStrip.Items.Add(CreateToolbarButton("Edit Manual", editManualAgentButton_Click, 100));
        agentsToolStrip.Items.Add(CreateToolbarButton("Remove Manual", removeManualAgentButton_Click, 110));

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
        processesToolStrip.Items.Add(CreateToolbarButton("Refresh", refreshProcessesButton_Click, 90));
        processesToolStrip.Items.Add(CreateToolbarButton("Terminate Selected", killProcessButton_Click, 150));

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
        filesToolStrip.Items.Add(CreateToolbarButton("Refresh Both", refreshFilesButton_Click, 110));
        filesToolStrip.Items.Add(new ToolStripSeparator());
        filesToolStrip.Items.Add(CreateToolbarButton("Upload", uploadButton_Click, 80));
        filesToolStrip.Items.Add(CreateToolbarButton("Download", downloadButton_Click, 90));
        filesToolStrip.Items.Add(new ToolStripSeparator());
        filesToolStrip.Items.Add(CreateToolbarButton("Delete Local", deleteLocalButton_Click, 95));
        filesToolStrip.Items.Add(CreateToolbarButton("Delete Remote", deleteRemoteButton_Click, 110));
        filesToolStrip.Items.Add(CreateToolbarButton("New Folder", newRemoteFolderButton_Click, 90));

        var filesSplitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 720,
            BackColor = Color.FromArgb(220, 224, 229),
            Panel1MinSize = 420,
            Panel2MinSize = 420
        };

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

        filesSplitContainer.Panel1.Controls.Add(localPanelLayout);
        filesSplitContainer.Panel2.Controls.Add(remotePanelLayout);

        filesLayout.Controls.Add(filesToolStrip, 0, 0);
        filesLayout.Controls.Add(filesSplitContainer, 0, 1);
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
            Height = 42,
            Padding = new Padding(4),
            BackColor = Color.White,
            RenderMode = ToolStripRenderMode.System
        };
    }

    private static ToolStripButton CreateToolbarButton(string text, EventHandler onClick, int width)
    {
        var button = new ToolStripButton(text)
        {
            DisplayStyle = ToolStripItemDisplayStyle.Text,
            AutoSize = false,
            Width = width
        };
        button.Click += onClick;
        return button;
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
