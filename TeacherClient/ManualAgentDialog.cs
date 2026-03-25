#nullable enable

using TeacherClient.Models;

namespace TeacherClient;

public partial class ManualAgentDialog : Form
{
    public ManualAgentDialog()
    {
        InitializeComponent();
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
            MacAddress = macAddressTextBox.Text.Trim(),
            Notes = notesTextBox.Text.Trim()
        };
    }

    private void saveButton_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(displayNameTextBox.Text))
        {
            MessageBox.Show("Display name is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(ipAddressTextBox.Text))
        {
            MessageBox.Show("IP address is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
}
