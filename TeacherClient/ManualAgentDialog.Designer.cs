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

        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.White;
        Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        Text = "Manual Agent";
        Width = 720;
        Height = 560;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        Padding = new Padding(20, 18, 20, 18);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 6
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 140F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 76F));

        ConfigureFieldLabel(displayNameLabel, "Display name");
        ConfigureFieldLabel(ipAddressLabel, "IP address");
        ConfigureFieldLabel(groupNameLabel, "Group");
        ConfigureFieldLabel(macAddressLabel, "MAC address");
        ConfigureFieldLabel(notesLabel, "Notes");

        displayNameTextBox.Dock = DockStyle.Fill;
        displayNameTextBox.MinimumSize = new Size(0, 42);
        displayNameTextBox.Margin = new Padding(0, 6, 0, 6);

        var ipLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            Margin = new Padding(0, 6, 0, 6)
        };
        ipLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        ipLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 24F));
        ipLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70F));
        ipLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112F));

        ipAddressTextBox.Dock = DockStyle.Fill;
        ipAddressTextBox.MinimumSize = new Size(0, 45);
        ipAddressTextBox.Margin = new Padding(0, 0, 12, 0);

        portLabel.Dock = DockStyle.Fill;
        portLabel.Text = "Port";
        portLabel.TextAlign = ContentAlignment.MiddleLeft;
        portLabel.Margin = new Padding(0);

        portNumericUpDown.Dock = DockStyle.Fill;
        portNumericUpDown.Minimum = 1;
        portNumericUpDown.Maximum = 65535;
        portNumericUpDown.Value = 5055;
        portNumericUpDown.Margin = new Padding(0, 2, 0, 2);
        portNumericUpDown.MinimumSize = new Size(0, 45);

        ipLayout.Controls.Add(ipAddressTextBox, 0, 0);
        ipLayout.Controls.Add(new Panel(), 1, 0);
        ipLayout.Controls.Add(portLabel, 2, 0);
        ipLayout.Controls.Add(portNumericUpDown, 3, 0);

        groupNameTextBox.Dock = DockStyle.Fill;
        groupNameTextBox.MinimumSize = new Size(0, 45);
        groupNameTextBox.Margin = new Padding(0, 6, 0, 6);

        macAddressTextBox.Dock = DockStyle.Fill;
        macAddressTextBox.MinimumSize = new Size(0, 45);
        macAddressTextBox.Margin = new Padding(0, 6, 0, 6);

        notesTextBox.Dock = DockStyle.Fill;
        notesTextBox.Multiline = true;
        notesTextBox.ScrollBars = ScrollBars.Vertical;
        notesTextBox.Margin = new Padding(0, 6, 0, 6);

        var buttonsLayout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 16, 0, 0),
            WrapContents = false
        };

        saveButton.Text = "Save";
        saveButton.Width = 120;
        saveButton.Height = 45;
        saveButton.Margin = new Padding(12, 0, 0, 0);
        saveButton.Click += saveButton_Click;

        cancelButton.Text = "Cancel";
        cancelButton.Width = 120;
        cancelButton.Height = 45;
        cancelButton.Margin = new Padding(12, 0, 0, 0);
        cancelButton.Click += cancelButton_Click;

        buttonsLayout.Controls.Add(cancelButton);
        buttonsLayout.Controls.Add(saveButton);

        layout.Controls.Add(displayNameLabel, 0, 0);
        layout.Controls.Add(displayNameTextBox, 1, 0);
        layout.Controls.Add(ipAddressLabel, 0, 1);
        layout.Controls.Add(ipLayout, 1, 1);
        layout.Controls.Add(groupNameLabel, 0, 2);
        layout.Controls.Add(groupNameTextBox, 1, 2);
        layout.Controls.Add(macAddressLabel, 0, 3);
        layout.Controls.Add(macAddressTextBox, 1, 3);
        layout.Controls.Add(notesLabel, 0, 4);
        layout.Controls.Add(notesTextBox, 1, 4);
        layout.Controls.Add(buttonsLayout, 1, 5);

        Controls.Add(layout);

        AcceptButton = saveButton;
        CancelButton = cancelButton;

        ((System.ComponentModel.ISupportInitialize)portNumericUpDown).EndInit();
        ResumeLayout(false);
    }

    private static void ConfigureFieldLabel(Label label, string text)
    {
        label.Text = text;
        label.Dock = DockStyle.Fill;
        label.TextAlign = ContentAlignment.MiddleLeft;
        label.Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        label.Margin = new Padding(0, 0, 12, 0);
    }
}
