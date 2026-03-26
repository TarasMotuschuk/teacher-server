#nullable enable

namespace StudentAgent.UI;

public partial class SettingsForm
{
    private System.ComponentModel.IContainer? components = null;
    private Label sharedSecretLabel = null!;
    private TextBox sharedSecretTextBox = null!;
    private Label passwordLabel = null!;
    private TextBox passwordTextBox = null!;
    private Label confirmPasswordLabel = null!;
    private TextBox confirmPasswordTextBox = null!;
    private Label languageLabel = null!;
    private ComboBox languageComboBox = null!;
    private Button clearLogsButton = null!;
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
        passwordLabel = new Label();
        passwordTextBox = new TextBox();
        confirmPasswordLabel = new Label();
        confirmPasswordTextBox = new TextBox();
        languageLabel = new Label();
        languageComboBox = new ComboBox();
        clearLogsButton = new Button();
        saveButton = new Button();
        cancelButton = new Button();
        SuspendLayout();

        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.White;
        Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        Text = "StudentAgent Settings";
        Width = 640;
        Height = 420;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Padding = new Padding(22, 20, 22, 18);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 5
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170F));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 86F));

        sharedSecretLabel.Dock = DockStyle.Fill;
        sharedSecretLabel.Text = "Shared secret";
        sharedSecretLabel.TextAlign = ContentAlignment.MiddleLeft;
        sharedSecretLabel.Margin = new Padding(0, 0, 12, 0);

        sharedSecretTextBox.Dock = DockStyle.Fill;
        sharedSecretTextBox.MinimumSize = new Size(0, 45);
        sharedSecretTextBox.Margin = new Padding(0, 8, 0, 8);
        sharedSecretTextBox.AutoSize = false;

        passwordLabel.Dock = DockStyle.Fill;
        passwordLabel.Text = "New password";
        passwordLabel.TextAlign = ContentAlignment.MiddleLeft;
        passwordLabel.Margin = new Padding(0, 0, 12, 0);

        passwordTextBox.Dock = DockStyle.Fill;
        passwordTextBox.MinimumSize = new Size(0, 45);
        passwordTextBox.Margin = new Padding(0, 8, 0, 8);
        passwordTextBox.AutoSize = false;
        passwordTextBox.UseSystemPasswordChar = true;

        confirmPasswordLabel.Dock = DockStyle.Fill;
        confirmPasswordLabel.Text = "Confirm password";
        confirmPasswordLabel.TextAlign = ContentAlignment.MiddleLeft;
        confirmPasswordLabel.Margin = new Padding(0, 0, 12, 0);

        confirmPasswordTextBox.Dock = DockStyle.Fill;
        confirmPasswordTextBox.MinimumSize = new Size(0, 45);
        confirmPasswordTextBox.Margin = new Padding(0, 8, 0, 8);
        confirmPasswordTextBox.AutoSize = false;
        confirmPasswordTextBox.UseSystemPasswordChar = true;

        languageLabel.Dock = DockStyle.Fill;
        languageLabel.TextAlign = ContentAlignment.MiddleLeft;
        languageLabel.Margin = new Padding(0, 0, 12, 0);

        languageComboBox.Dock = DockStyle.Fill;
        languageComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        languageComboBox.MinimumSize = new Size(0, 45);
        languageComboBox.Margin = new Padding(0, 8, 0, 8);

        var buttonsLayout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 16, 0, 0)
        };

        clearLogsButton.Text = "Clear logs";
        clearLogsButton.Width = 120;
        clearLogsButton.Height = 45;
        clearLogsButton.Margin = new Padding(12, 0, 0, 0);
        clearLogsButton.Click += clearLogsButton_Click;

        saveButton.Text = "Save";
        saveButton.Width = 100;
        saveButton.Height = 45;
        saveButton.Margin = new Padding(12, 0, 0, 0);
        saveButton.Click += saveButton_Click;

        cancelButton.Text = "Cancel";
        cancelButton.Width = 100;
        cancelButton.Height = 45;
        cancelButton.Margin = new Padding(12, 0, 0, 0);
        cancelButton.Click += cancelButton_Click;

        buttonsLayout.Controls.Add(cancelButton);
        buttonsLayout.Controls.Add(saveButton);
        buttonsLayout.Controls.Add(clearLogsButton);

        layout.Controls.Add(sharedSecretLabel, 0, 0);
        layout.Controls.Add(sharedSecretTextBox, 1, 0);
        layout.Controls.Add(passwordLabel, 0, 1);
        layout.Controls.Add(passwordTextBox, 1, 1);
        layout.Controls.Add(confirmPasswordLabel, 0, 2);
        layout.Controls.Add(confirmPasswordTextBox, 1, 2);
        layout.Controls.Add(languageLabel, 0, 3);
        layout.Controls.Add(languageComboBox, 1, 3);
        layout.Controls.Add(buttonsLayout, 0, 4);
        layout.SetColumnSpan(buttonsLayout, 2);

        Controls.Add(layout);

        ResumeLayout(false);
    }
}
