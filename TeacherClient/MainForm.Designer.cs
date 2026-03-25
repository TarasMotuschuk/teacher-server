#nullable enable

namespace TeacherClient;

partial class MainForm
{
    private System.ComponentModel.IContainer? components = null;
    private MenuStrip mainMenuStrip = null!;
    private TabControl mainTabControl = null!;
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
        SuspendLayout();

        Text = "Teacher Classroom Client";
        Width = 1280;
        Height = 760;
        MinimumSize = new Size(1100, 700);
        MainMenuStrip = mainMenuStrip;

        var connectionMenuItem = new ToolStripMenuItem("Connection");
        connectionMenuItem.DropDownItems.Add("Connect", null, connectButton_Click);

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

        mainMenuStrip.Dock = DockStyle.Top;
        mainMenuStrip.Items.Add(connectionMenuItem);
        mainMenuStrip.Items.Add(processesMenuItem);
        mainMenuStrip.Items.Add(filesMenuItem);

        var topPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 68,
            Padding = new Padding(12, 10, 12, 10)
        };

        var headerLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 6,
            RowCount = 1
        };
        headerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
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
            AutoSize = true,
            Margin = new Padding(0, 8, 8, 0)
        };

        serverUrlTextBox.Dock = DockStyle.Fill;
        serverUrlTextBox.Margin = new Padding(0, 2, 12, 0);

        var secretLabel = new Label
        {
            Text = "Secret",
            Anchor = AnchorStyles.Left,
            AutoSize = true,
            Margin = new Padding(0, 8, 8, 0)
        };

        sharedSecretTextBox.Dock = DockStyle.Fill;
        sharedSecretTextBox.Margin = new Padding(0, 2, 12, 0);

        connectButton.Text = "Connect";
        connectButton.Dock = DockStyle.Fill;
        connectButton.Height = 34;
        connectButton.MinimumSize = new Size(0, 34);
        connectButton.Margin = new Padding(0, 0, 12, 0);
        connectButton.Click += connectButton_Click;

        statusLabel.Dock = DockStyle.Fill;
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
        mainTabControl.TabPages.Add(processesTabPage);
        mainTabControl.TabPages.Add(filesTabPage);

        processesTabPage.Text = "Processes";
        filesTabPage.Text = "Files";

        refreshProcessesButton.Text = "Refresh";
        refreshProcessesButton.Left = 12;
        refreshProcessesButton.Top = 12;
        refreshProcessesButton.Width = 100;
        refreshProcessesButton.Height = 34;
        refreshProcessesButton.Click += refreshProcessesButton_Click;

        killProcessButton.Text = "Terminate Selected";
        killProcessButton.Left = 124;
        killProcessButton.Top = 12;
        killProcessButton.Width = 150;
        killProcessButton.Height = 34;
        killProcessButton.Click += killProcessButton_Click;

        processesGrid.Left = 12;
        processesGrid.Top = 48;
        processesGrid.Width = 1220;
        processesGrid.Height = 610;
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
        refreshFilesButton.Height = 34;
        refreshFilesButton.Click += refreshFilesButton_Click;

        uploadButton.Text = "Upload ->";
        uploadButton.Left = 134;
        uploadButton.Top = 12;
        uploadButton.Width = 100;
        uploadButton.Height = 34;
        uploadButton.Click += uploadButton_Click;

        downloadButton.Text = "<- Download";
        downloadButton.Left = 246;
        downloadButton.Top = 12;
        downloadButton.Width = 110;
        downloadButton.Height = 34;
        downloadButton.Click += downloadButton_Click;

        deleteLocalButton.Text = "Delete Local";
        deleteLocalButton.Left = 368;
        deleteLocalButton.Top = 12;
        deleteLocalButton.Width = 100;
        deleteLocalButton.Height = 34;
        deleteLocalButton.Click += deleteLocalButton_Click;

        deleteRemoteButton.Text = "Delete Remote";
        deleteRemoteButton.Left = 480;
        deleteRemoteButton.Top = 12;
        deleteRemoteButton.Width = 110;
        deleteRemoteButton.Height = 34;
        deleteRemoteButton.Click += deleteRemoteButton_Click;

        newRemoteFolderButton.Text = "New Remote Folder";
        newRemoteFolderButton.Left = 602;
        newRemoteFolderButton.Top = 12;
        newRemoteFolderButton.Width = 140;
        newRemoteFolderButton.Height = 34;
        newRemoteFolderButton.Click += newRemoteFolderButton_Click;

        var localLabel = new Label
        {
            Text = "Teacher PC",
            Left = 12,
            Top = 52,
            AutoSize = true
        };

        upLocalButton.Text = "Up";
        upLocalButton.Left = 12;
        upLocalButton.Top = 74;
        upLocalButton.Width = 52;
        upLocalButton.Height = 32;
        upLocalButton.Click += upLocalButton_Click;

        localPathTextBox.Left = 72;
        localPathTextBox.Top = 76;
        localPathTextBox.Width = 500;
        localPathTextBox.ReadOnly = true;

        var remoteLabel = new Label
        {
            Text = "Student PC",
            Left = 628,
            Top = 52,
            AutoSize = true
        };

        upRemoteButton.Text = "Up";
        upRemoteButton.Left = 628;
        upRemoteButton.Top = 74;
        upRemoteButton.Width = 52;
        upRemoteButton.Height = 32;
        upRemoteButton.Click += upRemoteButton_Click;

        remotePathTextBox.Left = 688;
        remotePathTextBox.Top = 76;
        remotePathTextBox.Width = 500;
        remotePathTextBox.ReadOnly = true;

        localFilesGrid.Left = 12;
        localFilesGrid.Top = 108;
        localFilesGrid.Width = 560;
        localFilesGrid.Height = 550;
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
        remoteFilesGrid.Top = 108;
        remoteFilesGrid.Width = 560;
        remoteFilesGrid.Height = 550;
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
