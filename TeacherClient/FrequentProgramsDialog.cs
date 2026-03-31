#nullable enable

using Teacher.Common.Contracts;
using TeacherClient.Localization;
using TeacherClient.Models;

namespace TeacherClient;

public sealed class FrequentProgramsDialog : Form
{
    private readonly BindingSource _bindingSource = new();
    private readonly List<FrequentProgramEntry> _entries;
    private readonly DataGridView _grid;

    public FrequentProgramsDialog(IEnumerable<FrequentProgramEntry> entries)
    {
        _entries = entries.ToList();
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
        Text = TeacherClientText.FrequentProgramsTitle;
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(920, 600);
        MinimumSize = new Size(800, 520);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 1,
            RowCount = 3
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));

        var toolBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill
        };

        var addButton = new Button { Text = TeacherClientText.AddProgram, MinimumSize = new Size(120, 40), AutoSize = true };
        var removeButton = new Button { Text = TeacherClientText.RemoveProgram, MinimumSize = new Size(120, 40), AutoSize = true };
        addButton.Click += (_, _) => AddEntry();
        removeButton.Click += (_, _) => RemoveSelectedEntry();
        toolBar.Controls.Add(addButton);
        toolBar.Controls.Add(removeButton);

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false
        };
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = TeacherClientText.ProgramName, DataPropertyName = nameof(FrequentProgramEntry.DisplayName), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 30F });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = TeacherClientText.CommandText, DataPropertyName = nameof(FrequentProgramEntry.CommandText), AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, FillWeight = 55F });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = TeacherClientText.RunAs, DataPropertyName = nameof(FrequentProgramEntry.RunAs), Width = 140 });
        _bindingSource.DataSource = _entries;
        _grid.DataSource = _bindingSource;

        var bottomButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft
        };
        var closeButton = new Button { Text = TeacherClientText.Close, DialogResult = DialogResult.OK, MinimumSize = new Size(120, 42), AutoSize = true };
        bottomButtons.Controls.Add(closeButton);

        layout.Controls.Add(toolBar, 0, 0);
        layout.Controls.Add(_grid, 0, 1);
        layout.Controls.Add(bottomButtons, 0, 2);
        Controls.Add(layout);

        AcceptButton = closeButton;
        CancelButton = closeButton;
    }

    public IReadOnlyList<FrequentProgramEntry> Entries => _entries;

    private void AddEntry()
    {
        var form = new SimpleProgramEditorDialog();
        if (form.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _entries.Add(FrequentProgramEntry.Create(form.ProgramName, form.CommandText, form.RunAs));
        RefreshGrid();
    }

    private void RemoveSelectedEntry()
    {
        if (_grid.CurrentRow?.DataBoundItem is not FrequentProgramEntry entry)
        {
            MessageBox.Show(this, TeacherClientText.ChooseProgramFirst, TeacherClientText.Validation, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        _entries.RemoveAll(x => string.Equals(x.Id, entry.Id, StringComparison.OrdinalIgnoreCase));
        RefreshGrid();
    }

    private void RefreshGrid()
    {
        _bindingSource.DataSource = null;
        _bindingSource.DataSource = _entries
            .OrderBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.CommandText, StringComparer.OrdinalIgnoreCase)
            .ToList();
        _grid.DataSource = _bindingSource;
    }

    private sealed class SimpleProgramEditorDialog : Form
    {
        private readonly TextBox _nameTextBox;
        private readonly TextBox _commandTextBox;
        private readonly ComboBox _runAsComboBox;

        public SimpleProgramEditorDialog()
        {
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point);
            Text = TeacherClientText.AddProgram;
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(760, 360);

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 2, RowCount = 4 };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 140F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 54F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));

            _nameTextBox = new TextBox { Dock = DockStyle.Fill };
            _commandTextBox = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical };
            _runAsComboBox = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
            _runAsComboBox.Items.AddRange([TeacherClientText.RunAsCurrentUser, TeacherClientText.RunAsAdministrator]);
            _runAsComboBox.SelectedIndex = 0;

            layout.Controls.Add(new Label { Text = TeacherClientText.ProgramName, TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, 0);
            layout.Controls.Add(_nameTextBox, 1, 0);
            layout.Controls.Add(new Label { Text = TeacherClientText.CommandText, TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, 1);
            layout.Controls.Add(_commandTextBox, 1, 1);
            layout.Controls.Add(new Label { Text = TeacherClientText.RunAs, TextAlign = ContentAlignment.MiddleLeft, Dock = DockStyle.Fill }, 0, 2);
            layout.Controls.Add(_runAsComboBox, 1, 2);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var okButton = new Button { Text = TeacherClientText.Ok, DialogResult = DialogResult.OK, MinimumSize = new Size(120, 42), AutoSize = true };
            var cancelButton = new Button { Text = TeacherClientText.Cancel, DialogResult = DialogResult.Cancel, MinimumSize = new Size(120, 42), AutoSize = true };
            okButton.Click += (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(_nameTextBox.Text) || string.IsNullOrWhiteSpace(_commandTextBox.Text))
                {
                    MessageBox.Show(this, TeacherClientText.CommandScriptRequired, TeacherClientText.Validation, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    DialogResult = DialogResult.None;
                }
            };
            buttons.Controls.Add(okButton);
            buttons.Controls.Add(cancelButton);
            layout.Controls.Add(buttons, 1, 3);
            Controls.Add(layout);
            AcceptButton = okButton;
            CancelButton = cancelButton;
        }

        public string ProgramName => _nameTextBox.Text.Trim();
        public string CommandText => _commandTextBox.Text.Trim();
        public RemoteCommandRunAs RunAs => _runAsComboBox.SelectedIndex == 1 ? RemoteCommandRunAs.Administrator : RemoteCommandRunAs.CurrentUser;
    }
}
