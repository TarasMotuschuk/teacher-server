#nullable enable

namespace TeacherClient;

partial class SettingsDialog
{
    private System.ComponentModel.IContainer? components = null;
    private Label sharedSecretLabel = null!;
    private TextBox sharedSecretTextBox = null!;
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
        hintLabel = new Label();
        saveButton = new Button();
        cancelButton = new Button();
        SuspendLayout();

        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.White;
        Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        Text = "Teacher Client Settings";
        Width = 680;
        Height = 340;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Padding = new Padding(22, 20, 22, 18);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 3
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 86F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 72F));

        sharedSecretLabel.Dock = DockStyle.Fill;
        sharedSecretLabel.Text = "Shared secret";
        sharedSecretLabel.TextAlign = ContentAlignment.MiddleLeft;
        sharedSecretLabel.Margin = new Padding(0, 0, 12, 0);

        sharedSecretTextBox.Dock = DockStyle.Fill;
        sharedSecretTextBox.MinimumSize = new Size(0, 45);
        sharedSecretTextBox.Margin = new Padding(0, 8, 0, 8);

        hintLabel.Dock = DockStyle.Fill;
        hintLabel.Text = "This secret is used for agent discovery reachability checks and all teacher-to-student API calls.";
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
        layout.Controls.Add(hintLabel, 0, 1);
        layout.SetColumnSpan(hintLabel, 2);
        layout.Controls.Add(buttonsPanel, 0, 2);
        layout.SetColumnSpan(buttonsPanel, 2);

        Controls.Add(layout);

        AcceptButton = saveButton;
        CancelButton = cancelButton;

        ResumeLayout(false);
    }
}
