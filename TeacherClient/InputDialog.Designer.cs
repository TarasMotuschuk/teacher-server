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

        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.White;
        Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        Text = "Input";
        Width = 920;
        Height = 340;
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
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 78F));

        promptLabel.Dock = DockStyle.Fill;
        promptLabel.TextAlign = ContentAlignment.MiddleLeft;
        promptLabel.Text = "Prompt";
        promptLabel.Font = new Font("Segoe UI", 10.5F, FontStyle.Bold, GraphicsUnit.Point);
        promptLabel.Margin = new Padding(0, 0, 0, 6);

        valueTextBox.Dock = DockStyle.Fill;
        valueTextBox.MinimumSize = new Size(0, 45);
        valueTextBox.Margin = new Padding(0, 4, 0, 10);

        var buttonsLayout = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0, 16, 0, 0),
            WrapContents = false
        };

        okButton.Text = "OK";
        okButton.Width = 110;
        okButton.Height = 45;
        okButton.DialogResult = DialogResult.OK;
        okButton.Margin = new Padding(12, 0, 0, 0);

        cancelButton.Text = "Cancel";
        cancelButton.Width = 110;
        cancelButton.Height = 45;
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
