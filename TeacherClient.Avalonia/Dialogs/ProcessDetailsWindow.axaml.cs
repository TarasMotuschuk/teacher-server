using Avalonia.Controls;
using Teacher.Common.Contracts;
using TeacherClient.CrossPlatform.Localization;

namespace TeacherClient.CrossPlatform.Dialogs;

public partial class ProcessDetailsWindow : Window
{
    public ProcessDetailsWindow()
    {
        InitializeComponent();
    }

    public ProcessDetailsWindow(ProcessDetailsDto details)
        : this()
    {
        Title = CrossPlatformText.ProcessDetailsTitle;
        KillButton.Content = CrossPlatformText.TerminateSelected;
        RestartButton.Content = CrossPlatformText.RestartSelected;
        CloseButton.Content = CrossPlatformText.Close;
        DetailsTextBox.Text = ProcessDetailsWindowTextFormatter.BuildText(details);
    }

    public ProcessActionRequested ActionRequested { get; private set; }

    public static async Task<ProcessActionRequested> ShowAsync(Window owner, ProcessDetailsDto details)
    {
        var dialog = new ProcessDetailsWindow(details);
        await dialog.ShowDialog(owner);
        return dialog.ActionRequested;
    }

    private void KillButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ActionRequested = ProcessActionRequested.Kill;
        Close();
    }

    private void RestartButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ActionRequested = ProcessActionRequested.Restart;
        Close();
    }

    private void CloseButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ActionRequested = ProcessActionRequested.None;
        Close();
    }
}
