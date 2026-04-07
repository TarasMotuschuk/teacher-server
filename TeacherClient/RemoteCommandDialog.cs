#nullable enable

using Teacher.Common.Contracts;
using TeacherClient.Localization;
using TeacherClient.Models;

namespace TeacherClient;

public sealed class RemoteCommandDialog : Form
{
    private readonly ComboBox _runAsComboBox;
    private readonly TextBox _scriptTextBox;
    private readonly ListBox _frequentProgramsListBox;

    public RemoteCommandDialog(IReadOnlyList<FrequentProgramEntry> frequentPrograms, string? initialScript = null)
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        Text = TeacherClientText.RemoteCommandTitle;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(860, 640);
        Size = new Size(920, 680);
        FormBorderStyle = FormBorderStyle.Sizable;
        MaximizeBox = true;
        MinimizeBox = false;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 1,
            RowCount = 5,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));

        var hintLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = TeacherClientText.RemoteCommandHint,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        var commandLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
        };
        commandLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64F));
        commandLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36F));

        _scriptTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            AcceptsReturn = true,
            AcceptsTab = true,
            WordWrap = false,
            Text = initialScript ?? string.Empty,
        };

        var sidePanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(12, 0, 0, 0),
        };
        sidePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
        sidePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
        sidePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
        sidePanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        sidePanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));

        var runAsLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = TeacherClientText.RunAs,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        _runAsComboBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
        };
        _runAsComboBox.Items.AddRange([
            TeacherClientText.RunAsCurrentUser,
            TeacherClientText.RunAsAdministrator
        ]);
        _runAsComboBox.SelectedIndex = 0;

        var frequentLabel = new Label
        {
            Dock = DockStyle.Fill,
            Text = TeacherClientText.FrequentProgramsTitle,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        _frequentProgramsListBox = new ListBox
        {
            Dock = DockStyle.Fill,
        };
        _frequentProgramsListBox.Items.AddRange(frequentPrograms.Cast<object>().ToArray());
        _frequentProgramsListBox.DisplayMember = nameof(FrequentProgramEntry.DisplayName);

        var insertButton = new Button
        {
            Dock = DockStyle.Right,
            Width = 180,
            Text = TeacherClientText.InsertSelected,
        };
        insertButton.Click += (_, _) =>
        {
            if (_frequentProgramsListBox.SelectedItem is not FrequentProgramEntry entry)
            {
                MessageBox.Show(this, TeacherClientText.ChooseProgramFirst, TeacherClientText.Validation, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!string.IsNullOrWhiteSpace(_scriptTextBox.Text))
            {
                _scriptTextBox.AppendText(Environment.NewLine);
            }

            _scriptTextBox.AppendText(entry.CommandText);
            _runAsComboBox.SelectedIndex = entry.RunAs == RemoteCommandRunAs.Administrator ? 1 : 0;
            _scriptTextBox.Focus();
            _scriptTextBox.SelectionStart = _scriptTextBox.TextLength;
        };

        sidePanel.Controls.Add(runAsLabel, 0, 0);
        sidePanel.Controls.Add(_runAsComboBox, 0, 1);
        sidePanel.Controls.Add(frequentLabel, 0, 2);
        sidePanel.Controls.Add(_frequentProgramsListBox, 0, 3);
        sidePanel.Controls.Add(insertButton, 0, 4);

        commandLayout.Controls.Add(_scriptTextBox, 0, 0);
        commandLayout.Controls.Add(sidePanel, 1, 0);

        var bottomButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
        };

        var okButton = new Button
        {
            Text = TeacherClientText.Ok,
            DialogResult = DialogResult.OK,
            MinimumSize = new Size(120, 42),
            AutoSize = true,
        };
        okButton.Click += (_, e) =>
        {
            if (string.IsNullOrWhiteSpace(_scriptTextBox.Text))
            {
                MessageBox.Show(this, TeacherClientText.CommandScriptRequired, TeacherClientText.Validation, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
            }
        };

        var cancelButton = new Button
        {
            Text = TeacherClientText.Cancel,
            DialogResult = DialogResult.Cancel,
            MinimumSize = new Size(120, 42),
            AutoSize = true,
        };

        bottomButtons.Controls.Add(okButton);
        bottomButtons.Controls.Add(cancelButton);

        layout.Controls.Add(
            new Label
        {
            Dock = DockStyle.Fill,
            Text = TeacherClientText.RemoteCommandScript,
            Font = new Font(Font, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
        }, 0, 0);
        layout.Controls.Add(hintLabel, 0, 1);
        layout.Controls.Add(commandLayout, 0, 3);
        layout.Controls.Add(bottomButtons, 0, 4);

        Controls.Add(layout);
        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    public string Script => _scriptTextBox.Text.Trim();

    public RemoteCommandRunAs RunAs
        => _runAsComboBox.SelectedIndex == 1 ? RemoteCommandRunAs.Administrator : RemoteCommandRunAs.CurrentUser;
}
