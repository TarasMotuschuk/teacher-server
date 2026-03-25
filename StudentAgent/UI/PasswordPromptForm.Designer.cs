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

        Text = "Enter password";
        Width = 360;
        Height = 170;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        passwordLabel.Left = 12;
        passwordLabel.Top = 18;
        passwordLabel.Width = 320;
        passwordLabel.Text = "Enter the StudentAgent admin password:";

        passwordTextBox.Left = 12;
        passwordTextBox.Top = 48;
        passwordTextBox.Width = 320;
        passwordTextBox.UseSystemPasswordChar = true;

        okButton.Text = "OK";
        okButton.Left = 176;
        okButton.Top = 84;
        okButton.Width = 75;
        okButton.Height = 32;
        okButton.DialogResult = DialogResult.OK;

        cancelButton.Text = "Cancel";
        cancelButton.Left = 257;
        cancelButton.Top = 84;
        cancelButton.Width = 75;
        cancelButton.Height = 32;
        cancelButton.DialogResult = DialogResult.Cancel;

        Controls.Add(passwordLabel);
        Controls.Add(passwordTextBox);
        Controls.Add(okButton);
        Controls.Add(cancelButton);

        AcceptButton = okButton;
        CancelButton = cancelButton;

        ResumeLayout(false);
    }
}
