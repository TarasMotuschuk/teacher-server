#nullable enable

namespace TeacherClient;

partial class ManualAgentDialog
{
    private System.ComponentModel.IContainer? components = null;
    private Label displayNameLabel = null!;
    private TextBox displayNameTextBox = null!;
    private Label ipAddressLabel = null!;
    private TextBox ipAddressTextBox = null!;
    private Label portLabel = null!;
    private NumericUpDown portNumericUpDown = null!;
    private Label groupNameLabel = null!;
    private TextBox groupNameTextBox = null!;
    private Label macAddressLabel = null!;
    private TextBox macAddressTextBox = null!;
    private Label notesLabel = null!;
    private TextBox notesTextBox = null!;
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
        displayNameLabel = new Label();
        displayNameTextBox = new TextBox();
        ipAddressLabel = new Label();
        ipAddressTextBox = new TextBox();
        portLabel = new Label();
        portNumericUpDown = new NumericUpDown();
        groupNameLabel = new Label();
        groupNameTextBox = new TextBox();
        macAddressLabel = new Label();
        macAddressTextBox = new TextBox();
        notesLabel = new Label();
        notesTextBox = new TextBox();
        saveButton = new Button();
        cancelButton = new Button();
        ((System.ComponentModel.ISupportInitialize)portNumericUpDown).BeginInit();
        SuspendLayout();

        Text = "Manual Agent";
        Width = 520;
        Height = 430;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;

        displayNameLabel.Left = 16;
        displayNameLabel.Top = 18;
        displayNameLabel.Width = 120;
        displayNameLabel.Height = 45;
        displayNameLabel.Text = "Display name";

        displayNameTextBox.Left = 144;
        displayNameTextBox.Top = 18;
        displayNameTextBox.Width = 340;
        displayNameTextBox.Height = 45;
        displayNameTextBox.AutoSize = false;

        ipAddressLabel.Left = 16;
        ipAddressLabel.Top = 72;
        ipAddressLabel.Width = 120;
        ipAddressLabel.Height = 45;
        ipAddressLabel.Text = "IP address";

        ipAddressTextBox.Left = 144;
        ipAddressTextBox.Top = 72;
        ipAddressTextBox.Width = 220;
        ipAddressTextBox.Height = 45;
        ipAddressTextBox.AutoSize = false;

        portLabel.Left = 372;
        portLabel.Top = 72;
        portLabel.Width = 48;
        portLabel.Height = 45;
        portLabel.Text = "Port";

        portNumericUpDown.Left = 424;
        portNumericUpDown.Top = 78;
        portNumericUpDown.Width = 60;
        portNumericUpDown.Minimum = 1;
        portNumericUpDown.Maximum = 65535;
        portNumericUpDown.Value = 5055;

        groupNameLabel.Left = 16;
        groupNameLabel.Top = 126;
        groupNameLabel.Width = 120;
        groupNameLabel.Height = 45;
        groupNameLabel.Text = "Group";

        groupNameTextBox.Left = 144;
        groupNameTextBox.Top = 126;
        groupNameTextBox.Width = 340;
        groupNameTextBox.Height = 45;
        groupNameTextBox.AutoSize = false;

        macAddressLabel.Left = 16;
        macAddressLabel.Top = 180;
        macAddressLabel.Width = 120;
        macAddressLabel.Height = 45;
        macAddressLabel.Text = "MAC address";

        macAddressTextBox.Left = 144;
        macAddressTextBox.Top = 180;
        macAddressTextBox.Width = 340;
        macAddressTextBox.Height = 45;
        macAddressTextBox.AutoSize = false;

        notesLabel.Left = 16;
        notesLabel.Top = 234;
        notesLabel.Width = 120;
        notesLabel.Height = 45;
        notesLabel.Text = "Notes";

        notesTextBox.Left = 144;
        notesTextBox.Top = 234;
        notesTextBox.Width = 340;
        notesTextBox.Height = 90;
        notesTextBox.Multiline = true;

        saveButton.Text = "Save";
        saveButton.Left = 314;
        saveButton.Top = 340;
        saveButton.Width = 80;
        saveButton.Height = 45;
        saveButton.Click += saveButton_Click;

        cancelButton.Text = "Cancel";
        cancelButton.Left = 404;
        cancelButton.Top = 340;
        cancelButton.Width = 80;
        cancelButton.Height = 45;
        cancelButton.Click += cancelButton_Click;

        Controls.Add(displayNameLabel);
        Controls.Add(displayNameTextBox);
        Controls.Add(ipAddressLabel);
        Controls.Add(ipAddressTextBox);
        Controls.Add(portLabel);
        Controls.Add(portNumericUpDown);
        Controls.Add(groupNameLabel);
        Controls.Add(groupNameTextBox);
        Controls.Add(macAddressLabel);
        Controls.Add(macAddressTextBox);
        Controls.Add(notesLabel);
        Controls.Add(notesTextBox);
        Controls.Add(saveButton);
        Controls.Add(cancelButton);

        AcceptButton = saveButton;
        CancelButton = cancelButton;

        ((System.ComponentModel.ISupportInitialize)portNumericUpDown).EndInit();
        ResumeLayout(false);
    }
}
