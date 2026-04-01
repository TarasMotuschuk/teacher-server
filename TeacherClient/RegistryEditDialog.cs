#nullable enable

using Teacher.Common.Contracts;
using TeacherClient.Localization;

namespace TeacherClient;

public sealed class RegistryEditDialog : Form
{
    private readonly TextBox _nameTextBox;
    private readonly ComboBox _typeComboBox;
    private readonly TextBox _dataTextBox;

    public RegistryEditDialog(string? initialName = null, string? initialType = null, string? initialData = null)
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        Text = TeacherClientText.EditValue;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(520, 340);
        Size = new Size(580, 380);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 1,
            RowCount = 4
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));

        var nameLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        nameLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
        nameLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        var nameLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = TeacherClientText.ValueName,
            TextAlign = ContentAlignment.MiddleLeft
        };

        _nameTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Text = initialName ?? string.Empty
        };

        nameLayout.Controls.Add(nameLabel, 0, 0);
        nameLayout.Controls.Add(_nameTextBox, 1, 0);

        var typeLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        typeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120F));
        typeLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));

        var typeLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = TeacherClientText.ValueType,
            TextAlign = ContentAlignment.MiddleLeft
        };

        _typeComboBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _typeComboBox.Items.AddRange([
            "REG_SZ",
            "REG_EXPAND_SZ",
            "REG_DWORD",
            "REG_QWORD",
            "REG_BINARY",
            "REG_MULTI_SZ"
        ]);
        _typeComboBox.SelectedItem = initialType ?? "REG_SZ";

        typeLayout.Controls.Add(typeLabel, 0, 0);
        typeLayout.Controls.Add(_typeComboBox, 1, 0);

        var dataLabel = new Label
        {
            Dock = DockStyle.Top,
            Text = TeacherClientText.ValueData,
            TextAlign = ContentAlignment.MiddleLeft,
            Height = 20
        };

        _dataTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            AcceptsReturn = true,
            WordWrap = false,
            Text = initialData ?? string.Empty
        };

        var dataPanel = new Panel { Dock = DockStyle.Fill };
        dataPanel.Controls.Add(_dataTextBox);
        dataPanel.Controls.Add(dataLabel);

        var bottomButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft
        };

        var okButton = new Button
        {
            Text = TeacherClientText.Ok,
            DialogResult = DialogResult.OK,
            MinimumSize = new Size(120, 42),
            AutoSize = true
        };
        okButton.Click += (_, e) =>
        {
            if (string.IsNullOrWhiteSpace(_nameTextBox.Text))
            {
                MessageBox.Show(this, TeacherClientText.ValueName + " " + TeacherClientText.Required, TeacherClientText.Validation, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
            }
        };

        var cancelButton = new Button
        {
            Text = TeacherClientText.Cancel,
            DialogResult = DialogResult.Cancel,
            MinimumSize = new Size(120, 42),
            AutoSize = true
        };

        bottomButtons.Controls.Add(okButton);
        bottomButtons.Controls.Add(cancelButton);

        layout.Controls.Add(nameLayout, 0, 0);
        layout.Controls.Add(typeLayout, 0, 1);
        layout.Controls.Add(dataPanel, 0, 2);
        layout.Controls.Add(bottomButtons, 0, 3);

        Controls.Add(layout);
        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    public string ValueName => _nameTextBox.Text.Trim();
    public string ValueType => _typeComboBox.SelectedItem?.ToString() ?? "REG_SZ";
    public string ValueData => _dataTextBox.Text;
}
