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

        Text = "Teacher Classroom Client";
        Width = 1280;
        Height = 760;
        MinimumSize = new Size(1100, 700);
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
        mainMenuStrip.Items.Add(connectionMenuItem);
        mainMenuStrip.Items.Add(processesMenuItem);
        mainMenuStrip.Items.Add(filesMenuItem);
        mainMenuStrip.Items.Add(helpMenuItem);

        var topPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 86,
            Padding = new Padding(12, 10, 12, 10)
        };

        var headerLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 1
        };
        headerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45F));
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 290));
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var serverLabel = new Label
        {
            Text = "Server URL",
            Anchor = AnchorStyles.Left,
            AutoSize = false,
            Height = 45,
            Margin = new Padding(0, 8, 8, 0)
        };

        serverUrlTextBox.Dock = DockStyle.Fill;
        serverUrlTextBox.AutoSize = false;
        serverUrlTextBox.Height = 45;
        serverUrlTextBox.Margin = new Padding(0, 2, 12, 0);

        var secretLabel = new Label
        {
            Text = "Secret",
            Anchor = AnchorStyles.Left,
            AutoSize = false,
            Height = 45,
            Margin = new Padding(0, 8, 8, 0)
        };

        sharedSecretTextBox.Dock = DockStyle.Fill;
        sharedSecretTextBox.AutoSize = false;
        sharedSecretTextBox.Height = 45;
        sharedSecretTextBox.Margin = new Padding(0, 2, 12, 0);

        connectButton.Text = "Connect";
        connectButton.Dock = DockStyle.Fill;
        connectButton.Height = 45;
        connectButton.MinimumSize = new Size(0, 45);
        connectButton.Margin = new Padding(0, 0, 12, 0);
        connectButton.Click += connectButton_Click;

        statusLabel.Dock = DockStyle.Fill;
        statusLabel.Height = 45;
        statusLabel.TextAlign = ContentAlignment.MiddleLeft;
        statusLabel.Text = "Ready";

        headerLayout.Controls.Add(serverLabel, 0, 0);
        headerLayout.Controls.Add(serverUrlTextBox, 1, 0);
        headerLayout.Controls.Add(secretLabel, 2, 0);
        headerLayout.Controls.Add(sharedSecretTextBox, 3, 0);
        headerLayout.Controls.Add(connectButton, 4, 0);
        headerLayout.Controls.Add(statusLabel, 5, 0);
        topPanel.Controls.Add(headerLayout);

        mainTabControl.Dock = DockStyle.Fill;
        mainTabControl.TabPages.Add(agentsTabPage);
        mainTabControl.TabPages.Add(processesTabPage);
        mainTabControl.TabPages.Add(filesTabPage);

        agentsTabPage.Text = "Agents";
        processesTabPage.Text = "Processes";
        filesTabPage.Text = "Files";

        refreshAgentsButton.Text = "Refresh Agents";
        refreshAgentsButton.Left = 12;
        refreshAgentsButton.Top = 12;
        refreshAgentsButton.Width = 140;
        refreshAgentsButton.Height = 45;
        refreshAgentsButton.Click += refreshAgentsButton_Click;

        connectSelectedAgentButton.Text = "Connect Selected";
        connectSelectedAgentButton.Left = 164;
        connectSelectedAgentButton.Top = 12;
        connectSelectedAgentButton.Width = 150;
        connectSelectedAgentButton.Height = 45;
        connectSelectedAgentButton.Click += connectSelectedAgentButton_Click;

        addManualAgentButton.Text = "Add Manual";
        addManualAgentButton.Left = 326;
        addManualAgentButton.Top = 12;
        addManualAgentButton.Width = 120;
        addManualAgentButton.Height = 45;
        addManualAgentButton.Click += addManualAgentButton_Click;

        editManualAgentButton.Text = "Edit Manual";
        editManualAgentButton.Left = 458;
        editManualAgentButton.Top = 12;
        editManualAgentButton.Width = 120;
        editManualAgentButton.Height = 45;
        editManualAgentButton.Click += editManualAgentButton_Click;

        removeManualAgentButton.Text = "Remove Manual";
        removeManualAgentButton.Left = 590;
        removeManualAgentButton.Top = 12;
        removeManualAgentButton.Width = 140;
        removeManualAgentButton.Height = 45;
        removeManualAgentButton.Click += removeManualAgentButton_Click;

        agentSearchTextBox.Left = 744;
        agentSearchTextBox.Top = 12;
        agentSearchTextBox.Width = 180;
        agentSearchTextBox.Height = 45;
        agentSearchTextBox.AutoSize = false;
        agentSearchTextBox.TextChanged += agentFilters_Changed;

        groupFilterComboBox.Left = 936;
        groupFilterComboBox.Top = 12;
        groupFilterComboBox.Width = 130;
        groupFilterComboBox.Height = 45;
        groupFilterComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        groupFilterComboBox.SelectedIndexChanged += agentFilters_Changed;

        statusFilterComboBox.Left = 1078;
        statusFilterComboBox.Top = 12;
        statusFilterComboBox.Width = 90;
        statusFilterComboBox.Height = 45;
        statusFilterComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        statusFilterComboBox.SelectedIndexChanged += agentFilters_Changed;

        autoReconnectCheckBox.Left = 1180;
        autoReconnectCheckBox.Top = 18;
        autoReconnectCheckBox.Width = 180;
        autoReconnectCheckBox.Height = 45;
        autoReconnectCheckBox.Text = "Auto-reconnect";

        agentsGrid.Left = 12;
        agentsGrid.Top = 70;
        agentsGrid.Width = 1220;
        agentsGrid.Height = 588;
        agentsGrid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        agentsGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        agentsGrid.MultiSelect = false;
        agentsGrid.ReadOnly = true;
        agentsGrid.AllowUserToAddRows = false;
        agentsGrid.AllowUserToDeleteRows = false;
        agentsGrid.RowHeadersVisible = false;
        agentsGrid.AutoGenerateColumns = false;
        agentsGrid.CellDoubleClick += agentsGrid_CellDoubleClick;
        agentsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Source", DataPropertyName = "Source", Width = 90 });
        agentsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Status", DataPropertyName = "Status", Width = 90 });
        agentsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Group", DataPropertyName = "GroupName", Width = 120 });
        agentsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Machine", DataPropertyName = "MachineName", Width = 160 });
        agentsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "User", DataPropertyName = "CurrentUser", Width = 140 });
        agentsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "IP", DataPropertyName = "RespondingAddress", Width = 130 });
        agentsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Port", DataPropertyName = "Port", Width = 70 });
        agentsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "MACs", DataPropertyName = "MacAddressesDisplay", Width = 200 });
        agentsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Notes", DataPropertyName = "Notes", Width = 140 });
        agentsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Version", DataPropertyName = "Version", Width = 100 });
        agentsGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Last Seen UTC", DataPropertyName = "LastSeenDisplay", Width = 180 });

        agentsTabPage.Controls.Add(refreshAgentsButton);
        agentsTabPage.Controls.Add(connectSelectedAgentButton);
        agentsTabPage.Controls.Add(addManualAgentButton);
        agentsTabPage.Controls.Add(editManualAgentButton);
        agentsTabPage.Controls.Add(removeManualAgentButton);
        agentsTabPage.Controls.Add(agentSearchTextBox);
        agentsTabPage.Controls.Add(groupFilterComboBox);
        agentsTabPage.Controls.Add(statusFilterComboBox);
        agentsTabPage.Controls.Add(autoReconnectCheckBox);
        agentsTabPage.Controls.Add(agentsGrid);

        refreshProcessesButton.Text = "Refresh";
        refreshProcessesButton.Left = 12;
        refreshProcessesButton.Top = 12;
        refreshProcessesButton.Width = 100;
        refreshProcessesButton.Height = 45;
        refreshProcessesButton.Click += refreshProcessesButton_Click;

        killProcessButton.Text = "Terminate Selected";
        killProcessButton.Left = 124;
        killProcessButton.Top = 12;
        killProcessButton.Width = 150;
        killProcessButton.Height = 45;
        killProcessButton.Click += killProcessButton_Click;

        processesGrid.Left = 12;
        processesGrid.Top = 60;
        processesGrid.Width = 1220;
        processesGrid.Height = 598;
        processesGrid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        processesGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        processesGrid.MultiSelect = false;
        processesGrid.ReadOnly = true;
        processesGrid.AllowUserToAddRows = false;
        processesGrid.AllowUserToDeleteRows = false;
        processesGrid.RowHeadersVisible = false;
        processesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "PID", DataPropertyName = "Id", Width = 90 });
        processesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Process", DataPropertyName = "Name", Width = 220 });
        processesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Window", DataPropertyName = "MainWindowTitle", Width = 380 });
        processesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Memory", DataPropertyName = "WorkingSetBytes", Width = 120 });
        processesGrid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Visible", DataPropertyName = "HasVisibleWindow", Width = 80 });
        processesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Started UTC", DataPropertyName = "StartTimeUtc", Width = 180 });

        processesTabPage.Controls.Add(refreshProcessesButton);
        processesTabPage.Controls.Add(killProcessButton);
        processesTabPage.Controls.Add(processesGrid);

        refreshFilesButton.Text = "Refresh Both";
        refreshFilesButton.Left = 12;
        refreshFilesButton.Top = 12;
        refreshFilesButton.Width = 110;
        refreshFilesButton.Height = 45;
        refreshFilesButton.Click += refreshFilesButton_Click;

        uploadButton.Text = "Upload ->";
        uploadButton.Left = 134;
        uploadButton.Top = 12;
        uploadButton.Width = 100;
        uploadButton.Height = 45;
        uploadButton.Click += uploadButton_Click;

        downloadButton.Text = "<- Download";
        downloadButton.Left = 246;
        downloadButton.Top = 12;
        downloadButton.Width = 110;
        downloadButton.Height = 45;
        downloadButton.Click += downloadButton_Click;

        deleteLocalButton.Text = "Delete Local";
        deleteLocalButton.Left = 368;
        deleteLocalButton.Top = 12;
        deleteLocalButton.Width = 100;
        deleteLocalButton.Height = 45;
        deleteLocalButton.Click += deleteLocalButton_Click;

        deleteRemoteButton.Text = "Delete Remote";
        deleteRemoteButton.Left = 480;
        deleteRemoteButton.Top = 12;
        deleteRemoteButton.Width = 110;
        deleteRemoteButton.Height = 45;
        deleteRemoteButton.Click += deleteRemoteButton_Click;

        newRemoteFolderButton.Text = "New Remote Folder";
        newRemoteFolderButton.Left = 602;
        newRemoteFolderButton.Top = 12;
        newRemoteFolderButton.Width = 140;
        newRemoteFolderButton.Height = 45;
        newRemoteFolderButton.Click += newRemoteFolderButton_Click;

        var localLabel = new Label
        {
            Text = "Teacher PC",
            Left = 12,
            Top = 64,
            Width = 200,
            Height = 45,
            AutoSize = false
        };

        upLocalButton.Text = "Up";
        upLocalButton.Left = 12;
        upLocalButton.Top = 112;
        upLocalButton.Width = 52;
        upLocalButton.Height = 45;
        upLocalButton.Click += upLocalButton_Click;

        localPathTextBox.Left = 72;
        localPathTextBox.Top = 118;
        localPathTextBox.Width = 500;
        localPathTextBox.Height = 45;
        localPathTextBox.AutoSize = false;
        localPathTextBox.ReadOnly = true;

        var remoteLabel = new Label
        {
            Text = "Student PC",
            Left = 628,
            Top = 64,
            Width = 200,
            Height = 45,
            AutoSize = false
        };

        upRemoteButton.Text = "Up";
        upRemoteButton.Left = 628;
        upRemoteButton.Top = 112;
        upRemoteButton.Width = 52;
        upRemoteButton.Height = 45;
        upRemoteButton.Click += upRemoteButton_Click;

        remotePathTextBox.Left = 688;
        remotePathTextBox.Top = 118;
        remotePathTextBox.Width = 500;
        remotePathTextBox.Height = 45;
        remotePathTextBox.AutoSize = false;
        remotePathTextBox.ReadOnly = true;

        localFilesGrid.Left = 12;
        localFilesGrid.Top = 172;
        localFilesGrid.Width = 560;
        localFilesGrid.Height = 486;
        localFilesGrid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
        localFilesGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        localFilesGrid.MultiSelect = false;
        localFilesGrid.ReadOnly = true;
        localFilesGrid.AllowUserToAddRows = false;
        localFilesGrid.AllowUserToDeleteRows = false;
        localFilesGrid.RowHeadersVisible = false;
        localFilesGrid.CellDoubleClick += localFilesGrid_CellDoubleClick;
        localFilesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Name", DataPropertyName = "Name", Width = 220 });
        localFilesGrid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Dir", DataPropertyName = "IsDirectory", Width = 50 });
        localFilesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Size", DataPropertyName = "Size", Width = 90 });
        localFilesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Modified UTC", DataPropertyName = "LastModifiedUtc", Width = 180 });

        remoteFilesGrid.Left = 628;
        remoteFilesGrid.Top = 172;
        remoteFilesGrid.Width = 560;
        remoteFilesGrid.Height = 486;
        remoteFilesGrid.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        remoteFilesGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        remoteFilesGrid.MultiSelect = false;
        remoteFilesGrid.ReadOnly = true;
        remoteFilesGrid.AllowUserToAddRows = false;
        remoteFilesGrid.AllowUserToDeleteRows = false;
        remoteFilesGrid.RowHeadersVisible = false;
        remoteFilesGrid.CellDoubleClick += remoteFilesGrid_CellDoubleClick;
        remoteFilesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Name", DataPropertyName = "Name", Width = 220 });
        remoteFilesGrid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Dir", DataPropertyName = "IsDirectory", Width = 50 });
        remoteFilesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Size", DataPropertyName = "Size", Width = 90 });
        remoteFilesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Modified UTC", DataPropertyName = "LastModifiedUtc", Width = 180 });

        filesTabPage.Controls.Add(refreshFilesButton);
        filesTabPage.Controls.Add(uploadButton);
        filesTabPage.Controls.Add(downloadButton);
        filesTabPage.Controls.Add(deleteLocalButton);
        filesTabPage.Controls.Add(deleteRemoteButton);
        filesTabPage.Controls.Add(newRemoteFolderButton);
        filesTabPage.Controls.Add(localLabel);
        filesTabPage.Controls.Add(upLocalButton);
        filesTabPage.Controls.Add(localPathTextBox);
        filesTabPage.Controls.Add(remoteLabel);
        filesTabPage.Controls.Add(upRemoteButton);
        filesTabPage.Controls.Add(remotePathTextBox);
        filesTabPage.Controls.Add(localFilesGrid);
        filesTabPage.Controls.Add(remoteFilesGrid);

        Controls.Add(mainTabControl);
        Controls.Add(topPanel);
        Controls.Add(mainMenuStrip);
        ResumeLayout(false);
        PerformLayout();
    }
}
