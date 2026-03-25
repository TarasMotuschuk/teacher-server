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
        Width = 380;
        Height = 230;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        passwordLabel.Left = 12;
        passwordLabel.Top = 18;
        passwordLabel.Width = 340;
        passwordLabel.Height = 45;
        passwordLabel.Text = "Enter the StudentAgent admin password:";

        passwordTextBox.Left = 12;
        passwordTextBox.Top = 68;
        passwordTextBox.Width = 340;
        passwordTextBox.Height = 45;
        passwordTextBox.UseSystemPasswordChar = true;

        okButton.Text = "OK";
        okButton.Left = 192;
        okButton.Top = 136;
        okButton.Width = 80;
        okButton.Height = 45;
        okButton.DialogResult = DialogResult.OK;

        cancelButton.Text = "Cancel";
        cancelButton.Left = 278;
        cancelButton.Top = 136;
        cancelButton.Width = 80;
        cancelButton.Height = 45;
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
