using Teacher.Common.Contracts;
using TeacherClient.Localization;

namespace TeacherClient;

public partial class ProcessDetailsDialog : Form
{
    public ProcessActionRequested ActionRequested { get; private set; }

    public ProcessDetailsDialog(ProcessDetailsDto details)
    {
        InitializeComponent();
        Icon = AppIconLoader.Load();
        Text = TeacherClientText.ProcessDetailsTitle;
        detailsTextBox.Text = ProcessDetailsDialogTextFormatter.BuildText(details);
    }

    private void KillButton_Click(object? sender, EventArgs e)
    {
        ActionRequested = ProcessActionRequested.Kill;
        DialogResult = DialogResult.OK;
        Close();
    }

    private void RestartButton_Click(object? sender, EventArgs e)
    {
        ActionRequested = ProcessActionRequested.Restart;
        DialogResult = DialogResult.OK;
        Close();
    }
}
