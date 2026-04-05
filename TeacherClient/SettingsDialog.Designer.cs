#nullable enable

namespace TeacherClient;

partial class SettingsDialog
{
    private System.ComponentModel.IContainer? components = null;
    private Label sharedSecretLabel = null!;
    private TextBox sharedSecretTextBox = null!;
    private Label bulkCopyDestinationPathLabel = null!;
    private TextBox bulkCopyDestinationPathTextBox = null!;
    private Label studentWorkRootPathLabel = null!;
    private TextBox studentWorkRootPathTextBox = null!;
    private Label studentWorkFolderNameLabel = null!;
    private TextBox studentWorkFolderNameTextBox = null!;
    private Label desktopIconAutoRestoreIntervalLabel = null!;
    private NumericUpDown desktopIconAutoRestoreIntervalNumeric = null!;
    private Label browserLockCheckIntervalLabel = null!;
    private NumericUpDown browserLockCheckIntervalNumeric = null!;
    private Label languageLabel = null!;
    private ComboBox languageComboBox = null!;
    private Label hintLabel = null!;
    private Button saveButton = null!;
    private Button cancelButton = null!;

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
        sharedSecretLabel = new Label();
        sharedSecretTextBox = new TextBox();
        bulkCopyDestinationPathLabel = new Label();
        bulkCopyDestinationPathTextBox = new TextBox();
        studentWorkRootPathLabel = new Label();
        studentWorkRootPathTextBox = new TextBox();
        studentWorkFolderNameLabel = new Label();
        studentWorkFolderNameTextBox = new TextBox();
        desktopIconAutoRestoreIntervalLabel = new Label();
        desktopIconAutoRestoreIntervalNumeric = new NumericUpDown();
        browserLockCheckIntervalLabel = new Label();
        browserLockCheckIntervalNumeric = new NumericUpDown();
        languageLabel = new Label();
        languageComboBox = new ComboBox();
        hintLabel = new Label();
        saveButton = new Button();
        cancelButton = new Button();
        SuspendLayout();

        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.White;
        Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        Text = "ClassCommander Settings";
        Width = 760;
        Height = 560;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Padding = new Padding(22, 20, 22, 18);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 9
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 118F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72F));

        sharedSecretLabel.Dock = DockStyle.Fill;
        sharedSecretLabel.Text = "Shared secret";
        sharedSecretLabel.TextAlign = ContentAlignment.MiddleLeft;
        sharedSecretLabel.Margin = new Padding(0, 0, 12, 0);

        sharedSecretTextBox.Dock = DockStyle.Fill;
        sharedSecretTextBox.MinimumSize = new Size(0, 45);
        sharedSecretTextBox.Margin = new Padding(0, 8, 0, 8);

        bulkCopyDestinationPathLabel.Dock = DockStyle.Fill;
        bulkCopyDestinationPathLabel.Text = "Student destination folder";
        bulkCopyDestinationPathLabel.TextAlign = ContentAlignment.MiddleLeft;
        bulkCopyDestinationPathLabel.Margin = new Padding(0, 0, 12, 0);

        bulkCopyDestinationPathTextBox.Dock = DockStyle.Fill;
        bulkCopyDestinationPathTextBox.MinimumSize = new Size(0, 45);
        bulkCopyDestinationPathTextBox.Margin = new Padding(0, 8, 0, 8);

        studentWorkRootPathLabel.Dock = DockStyle.Fill;
        studentWorkRootPathLabel.Text = "Student work base path";
        studentWorkRootPathLabel.TextAlign = ContentAlignment.MiddleLeft;
        studentWorkRootPathLabel.Margin = new Padding(0, 0, 12, 0);

        studentWorkRootPathTextBox.Dock = DockStyle.Fill;
        studentWorkRootPathTextBox.MinimumSize = new Size(0, 45);
        studentWorkRootPathTextBox.Margin = new Padding(0, 8, 0, 8);

        studentWorkFolderNameLabel.Dock = DockStyle.Fill;
        studentWorkFolderNameLabel.Text = "Student work folder name";
        studentWorkFolderNameLabel.TextAlign = ContentAlignment.MiddleLeft;
        studentWorkFolderNameLabel.Margin = new Padding(0, 0, 12, 0);

        studentWorkFolderNameTextBox.Dock = DockStyle.Fill;
        studentWorkFolderNameTextBox.MinimumSize = new Size(0, 45);
        studentWorkFolderNameTextBox.Margin = new Padding(0, 8, 0, 8);

        desktopIconAutoRestoreIntervalLabel.Dock = DockStyle.Fill;
        desktopIconAutoRestoreIntervalLabel.TextAlign = ContentAlignment.MiddleLeft;
        desktopIconAutoRestoreIntervalLabel.Margin = new Padding(0, 0, 12, 0);

        desktopIconAutoRestoreIntervalNumeric.Dock = DockStyle.Left;
        desktopIconAutoRestoreIntervalNumeric.Minimum = 1;
        desktopIconAutoRestoreIntervalNumeric.Maximum = 1440;
        desktopIconAutoRestoreIntervalNumeric.Width = 180;
        desktopIconAutoRestoreIntervalNumeric.Height = 45;
        desktopIconAutoRestoreIntervalNumeric.Margin = new Padding(0, 8, 0, 8);

        browserLockCheckIntervalLabel.Dock = DockStyle.Fill;
        browserLockCheckIntervalLabel.TextAlign = ContentAlignment.MiddleLeft;
        browserLockCheckIntervalLabel.Margin = new Padding(0, 0, 12, 0);

        browserLockCheckIntervalNumeric.Dock = DockStyle.Left;
        browserLockCheckIntervalNumeric.Minimum = 5;
        browserLockCheckIntervalNumeric.Maximum = 3600;
        browserLockCheckIntervalNumeric.Width = 180;
        browserLockCheckIntervalNumeric.Height = 45;
        browserLockCheckIntervalNumeric.Margin = new Padding(0, 8, 0, 8);

        languageLabel.Dock = DockStyle.Fill;
        languageLabel.TextAlign = ContentAlignment.MiddleLeft;
        languageLabel.Margin = new Padding(0, 0, 12, 0);

        languageComboBox.Dock = DockStyle.Fill;
        languageComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        languageComboBox.MinimumSize = new Size(0, 45);
        languageComboBox.Margin = new Padding(0, 8, 0, 8);

        hintLabel.Dock = DockStyle.Fill;
        hintLabel.Text = "The shared secret is used for reachability checks and all teacher-to-student API calls. The destination folder defines the starting path on student PCs for bulk file and folder distribution.";
        hintLabel.TextAlign = ContentAlignment.TopLeft;
        hintLabel.Margin = new Padding(0, 4, 0, 0);

        var buttonsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 14, 0, 0),
            AutoSize = false
        };

        saveButton.Text = "Save";
        saveButton.Width = 110;
        saveButton.Height = 45;
        saveButton.DialogResult = DialogResult.OK;

        cancelButton.Text = "Cancel";
        cancelButton.Width = 110;
        cancelButton.Height = 45;
        cancelButton.DialogResult = DialogResult.Cancel;

        buttonsPanel.Controls.Add(saveButton);
        buttonsPanel.Controls.Add(cancelButton);

        layout.Controls.Add(sharedSecretLabel, 0, 0);
        layout.Controls.Add(sharedSecretTextBox, 1, 0);
        layout.Controls.Add(bulkCopyDestinationPathLabel, 0, 1);
        layout.Controls.Add(bulkCopyDestinationPathTextBox, 1, 1);
        layout.Controls.Add(studentWorkRootPathLabel, 0, 2);
        layout.Controls.Add(studentWorkRootPathTextBox, 1, 2);
        layout.Controls.Add(studentWorkFolderNameLabel, 0, 3);
        layout.Controls.Add(studentWorkFolderNameTextBox, 1, 3);
        layout.Controls.Add(desktopIconAutoRestoreIntervalLabel, 0, 4);
        layout.Controls.Add(desktopIconAutoRestoreIntervalNumeric, 1, 4);
        layout.Controls.Add(browserLockCheckIntervalLabel, 0, 5);
        layout.Controls.Add(browserLockCheckIntervalNumeric, 1, 5);
        layout.Controls.Add(languageLabel, 0, 6);
        layout.Controls.Add(languageComboBox, 1, 6);
        layout.Controls.Add(hintLabel, 0, 7);
        layout.SetColumnSpan(hintLabel, 2);
        layout.Controls.Add(buttonsPanel, 0, 8);
        layout.SetColumnSpan(buttonsPanel, 2);

        Controls.Add(layout);

        AcceptButton = saveButton;
        CancelButton = cancelButton;

        ResumeLayout(false);
    }
}
