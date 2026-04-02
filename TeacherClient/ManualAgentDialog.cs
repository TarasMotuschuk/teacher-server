#nullable enable

using TeacherClient.Models;
using TeacherClient.Localization;

namespace TeacherClient;

public partial class ManualAgentDialog : Form
{
    public ManualAgentDialog()
    {
        InitializeComponent();
        Icon = AppIconLoader.Load();
        ApplyLocalization();
    }

    public ManualAgentDialog(ManualAgentEntry? entry)
        : this()
    {
        if (entry is null)
        {
            return;
        }

        displayNameTextBox.Text = entry.DisplayName;
        ipAddressTextBox.Text = entry.IpAddress;
        portNumericUpDown.Value = entry.Port;
        groupNameTextBox.Text = entry.GroupName;
        macAddressTextBox.Text = entry.MacAddress;
        notesTextBox.Text = entry.Notes;
    }

    public ManualAgentEntry ToEntry(string? existingId = null)
    {
        return new ManualAgentEntry
        {
            Id = existingId ?? Guid.NewGuid().ToString("N"),
            DisplayName = displayNameTextBox.Text.Trim(),
            IpAddress = ipAddressTextBox.Text.Trim(),
            Port = Decimal.ToInt32(portNumericUpDown.Value),
            GroupName = groupNameTextBox.Text.Trim(),
            MacAddress = macAddressTextBox.Text.Trim(),
            Notes = notesTextBox.Text.Trim()
        };
    }

    private void saveButton_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(displayNameTextBox.Text))
        {
            MessageBox.Show(TeacherClientText.DisplayNameRequired, TeacherClientText.Validation, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(ipAddressTextBox.Text))
        {
            MessageBox.Show(TeacherClientText.IpAddressRequired, TeacherClientText.Validation, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private void cancelButton_Click(object? sender, EventArgs e)
    {
        DialogResult = DialogResult.Cancel;
        Close();
    }

    private void ApplyLocalization()
    {
        Text = TeacherClientText.ManualAgentTitle;
        displayNameLabel.Text = TeacherClientText.DisplayName;
        ipAddressLabel.Text = TeacherClientText.IpAddress;
        portLabel.Text = TeacherClientText.Port;
        groupNameLabel.Text = TeacherClientText.Group;
        macAddressLabel.Text = TeacherClientText.MacAddress;
        notesLabel.Text = TeacherClientText.Notes;
        saveButton.Text = TeacherClientText.Save;
        cancelButton.Text = TeacherClientText.Cancel;
    }
}
