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

        AutoScaleMode = AutoScaleMode.Font;
        BackColor = Color.White;
        Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        Text = "Input";
        Width = 500;
        Height = 240;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        Padding = new Padding(18);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        promptLabel.Dock = DockStyle.Fill;
        promptLabel.TextAlign = ContentAlignment.MiddleLeft;
        promptLabel.Text = "Prompt";
        promptLabel.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold, GraphicsUnit.Point);

        valueTextBox.Dock = DockStyle.Fill;
        valueTextBox.MinimumSize = new Size(0, 42);
        valueTextBox.Margin = new Padding(0, 2, 0, 10);

        var buttonsLayout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 12, 0, 0)
        };

        okButton.Text = "OK";
        okButton.Width = 100;
        okButton.Height = 44;
        okButton.DialogResult = DialogResult.OK;
        okButton.Margin = new Padding(12, 0, 0, 0);

        cancelButton.Text = "Cancel";
        cancelButton.Width = 100;
        cancelButton.Height = 44;
        cancelButton.DialogResult = DialogResult.Cancel;
        cancelButton.Margin = new Padding(12, 0, 0, 0);

        buttonsLayout.Controls.Add(cancelButton);
        buttonsLayout.Controls.Add(okButton);

        layout.Controls.Add(promptLabel, 0, 0);
        layout.Controls.Add(valueTextBox, 0, 1);
        layout.Controls.Add(buttonsLayout, 0, 2);

        Controls.Add(layout);

        AcceptButton = okButton;
        CancelButton = cancelButton;

        ResumeLayout(false);
    }
}
