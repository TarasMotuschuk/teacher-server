#nullable enable

namespace TeacherClient;

partial class InputDialog
{
    private System.ComponentModel.IContainer? components = null;
    private Label promptLabel = null!;
    private TextBox valueTextBox = null!;
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
        promptLabel = new Label();
        valueTextBox = new TextBox();
        okButton = new Button();
        cancelButton = new Button();
        SuspendLayout();

        Text = "Input";
        Width = 420;
        Height = 210;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;

        promptLabel.Left = 12;
        promptLabel.Top = 14;
        promptLabel.Width = 380;
        promptLabel.Height = 45;
        promptLabel.Text = "Prompt";

        valueTextBox.Left = 12;
        valueTextBox.Top = 40;
        valueTextBox.Width = 380;
        valueTextBox.Height = 45;
        valueTextBox.AutoSize = false;

        okButton.Text = "OK";
        okButton.Left = 236;
        okButton.Width = 75;
        okButton.Top = 128;
        okButton.Height = 45;
        okButton.DialogResult = DialogResult.OK;

        cancelButton.Text = "Cancel";
        cancelButton.Left = 317;
        cancelButton.Width = 75;
        cancelButton.Top = 128;
        cancelButton.Height = 45;
        cancelButton.DialogResult = DialogResult.Cancel;

        Controls.Add(promptLabel);
        Controls.Add(valueTextBox);
        Controls.Add(okButton);
        Controls.Add(cancelButton);

        AcceptButton = okButton;
        CancelButton = cancelButton;

        ResumeLayout(false);
    }
}
