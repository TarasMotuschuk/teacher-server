#nullable enable

namespace StudentAgent.UI;

partial class PasswordPromptForm
{
    private System.ComponentModel.IContainer? components = null;
    private Label passwordLabel = null!;
    private TextBox passwordTextBox = null!;
    private Button okButton = null!;
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
        passwordLabel = new Label();
        passwordTextBox = new TextBox();
        okButton = new Button();
        cancelButton = new Button();
        SuspendLayout();

        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.White;
        Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        Text = "Enter password";
        Width = 560;
        Height = 320;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Padding = new Padding(20, 18, 20, 18);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 82F));

        passwordLabel.Dock = DockStyle.Fill;
        passwordLabel.Margin = new Padding(0, 0, 0, 6);
        passwordLabel.Text = "Enter the StudentAgent admin password:";
        passwordLabel.TextAlign = ContentAlignment.MiddleLeft;

        passwordTextBox.Dock = DockStyle.Fill;
        passwordTextBox.MinimumSize = new Size(0, 48);
        passwordTextBox.Margin = new Padding(0, 4, 0, 10);
        passwordTextBox.AutoSize = false;
        passwordTextBox.UseSystemPasswordChar = true;

        var buttonsLayout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 16, 0, 0)
        };

        okButton.Text = "OK";
        okButton.Width = 110;
        okButton.Height = 45;
        okButton.Margin = new Padding(12, 0, 0, 0);
        okButton.DialogResult = DialogResult.OK;

        cancelButton.Text = "Cancel";
        cancelButton.Width = 170;
        cancelButton.Height = 45;
        cancelButton.Margin = new Padding(12, 0, 0, 0);
        cancelButton.DialogResult = DialogResult.Cancel;

        buttonsLayout.Controls.Add(cancelButton);
        buttonsLayout.Controls.Add(okButton);

        layout.Controls.Add(passwordLabel, 0, 0);
        layout.Controls.Add(passwordTextBox, 0, 1);
        layout.Controls.Add(buttonsLayout, 0, 2);

        Controls.Add(layout);

        AcceptButton = okButton;
        CancelButton = cancelButton;

        ResumeLayout(false);
    }
}
