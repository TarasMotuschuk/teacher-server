using Avalonia.Controls;
using Avalonia.Interactivity;
using TeacherClient.CrossPlatform.Localization;

namespace TeacherClient.CrossPlatform.Dialogs;

public enum RemoteFileOpenChoice
{
    OnStudentPc,
    OnLocalPcViaTemp,
}

public partial class RemoteOpenChoiceDialog : Window
{
    public RemoteOpenChoiceDialog()
    {
        InitializeComponent();
        Title = CrossPlatformText.RemoteOpenChoiceTitle;
        DescriptionTextBlock.Text = CrossPlatformText.RemoteOpenChoiceDescription;
        StudentRadio.Content = CrossPlatformText.RemoteOpenOnStudentPc;
        LocalRadio.Content = CrossPlatformText.RemoteOpenOnLocalPc;
        OkButton.Content = CrossPlatformText.Ok;
        CancelButton.Content = CrossPlatformText.Cancel;
        StudentRadio.IsChecked = true;
    }

    public static Task<RemoteFileOpenChoice?> ShowAsync(Window owner)
    {
        var dialog = new RemoteOpenChoiceDialog();
        return dialog.ShowDialog<RemoteFileOpenChoice?>(owner);
    }

    private void OkButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var choice = StudentRadio.IsChecked == true
            ? RemoteFileOpenChoice.OnStudentPc
            : RemoteFileOpenChoice.OnLocalPcViaTemp;
        Close(choice);
    }

    private void CancelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }
}
