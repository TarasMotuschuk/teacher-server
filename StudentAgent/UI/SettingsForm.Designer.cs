#nullable enable

namespace StudentAgent.UI;

partial class SettingsForm
{
    private System.ComponentModel.IContainer? components = null;
    private Label sharedSecretLabel = null!;
    private TextBox sharedSecretTextBox = null!;
    private Label passwordLabel = null!;
    private TextBox passwordTextBox = null!;
    private Label confirmPasswordLabel = null!;
    private TextBox confirmPasswordTextBox = null!;
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
        clearLogsButton = new Button();
        saveButton = new Button();
        cancelButton = new Button();
        SuspendLayout();

        Text = "StudentAgent Settings";
        Width = 520;
        Height = 360;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        sharedSecretLabel.Left = 16;
        sharedSecretLabel.Top = 20;
        sharedSecretLabel.Width = 140;
        sharedSecretLabel.Height = 45;
        sharedSecretLabel.Text = "Shared secret";

        sharedSecretTextBox.Left = 160;
        sharedSecretTextBox.Top = 16;
        sharedSecretTextBox.Width = 320;
        sharedSecretTextBox.Height = 45;
        sharedSecretTextBox.AutoSize = false;

        passwordLabel.Left = 16;
        passwordLabel.Top = 72;
        passwordLabel.Width = 140;
        passwordLabel.Height = 45;
        passwordLabel.Text = "New password";

        passwordTextBox.Left = 160;
        passwordTextBox.Top = 68;
        passwordTextBox.Width = 320;
        passwordTextBox.Height = 45;
        passwordTextBox.AutoSize = false;
        passwordTextBox.UseSystemPasswordChar = true;

        confirmPasswordLabel.Left = 16;
        confirmPasswordLabel.Top = 128;
        confirmPasswordLabel.Width = 140;
        confirmPasswordLabel.Height = 45;
        confirmPasswordLabel.Text = "Confirm password";

        confirmPasswordTextBox.Left = 160;
        confirmPasswordTextBox.Top = 124;
        confirmPasswordTextBox.Width = 320;
        confirmPasswordTextBox.Height = 45;
        confirmPasswordTextBox.AutoSize = false;
        confirmPasswordTextBox.UseSystemPasswordChar = true;

        clearLogsButton.Text = "Clear logs";
        clearLogsButton.Left = 16;
        clearLogsButton.Top = 258;
        clearLogsButton.Width = 120;
        clearLogsButton.Height = 45;
        clearLogsButton.Click += clearLogsButton_Click;

        saveButton.Text = "Save";
        saveButton.Left = 324;
        saveButton.Top = 258;
        saveButton.Width = 75;
        saveButton.Height = 45;
        saveButton.Click += saveButton_Click;

        cancelButton.Text = "Cancel";
        cancelButton.Left = 405;
        cancelButton.Top = 258;
        cancelButton.Width = 75;
        cancelButton.Height = 45;
        cancelButton.Click += cancelButton_Click;

        Controls.Add(sharedSecretLabel);
        Controls.Add(sharedSecretTextBox);
        Controls.Add(passwordLabel);
        Controls.Add(passwordTextBox);
        Controls.Add(confirmPasswordLabel);
        Controls.Add(confirmPasswordTextBox);
        Controls.Add(clearLogsButton);
        Controls.Add(saveButton);
        Controls.Add(cancelButton);

        ResumeLayout(false);
    }
}
