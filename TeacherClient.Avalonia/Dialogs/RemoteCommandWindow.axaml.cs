using Avalonia.Controls;
using Teacher.Common.Contracts;
using TeacherClient.CrossPlatform.Localization;
using TeacherClient.CrossPlatform.Models;

namespace TeacherClient.CrossPlatform.Dialogs;

public partial class RemoteCommandWindow : Window
{
    public RemoteCommandWindow()
    {
        InitializeComponent();
        Title = CrossPlatformText.RemoteCommandTitle;
        ScriptLabelTextBlock.Text = CrossPlatformText.RemoteCommandScript;
        HintTextBlock.Text = CrossPlatformText.RemoteCommandHint;
        RunAsLabelTextBlock.Text = CrossPlatformText.RunAs;
        FrequentProgramsLabelTextBlock.Text = CrossPlatformText.FrequentProgramsTitle;
        InsertSelectedButton.Content = CrossPlatformText.InsertSelected;
        OkButton.Content = CrossPlatformText.Ok;
        CancelButton.Content = CrossPlatformText.Cancel;
        RunAsComboBox.ItemsSource = new[]
        {
            CrossPlatformText.RunAsCurrentUser,
            CrossPlatformText.RunAsAdministrator,
        };
        RunAsComboBox.SelectedIndex = 0;
    }

    public static async Task<RemoteCommandSubmission?> ShowAsync(Window owner, IReadOnlyList<FrequentProgramEntry> frequentPrograms, string? initialScript = null)
    {
        var dialog = new RemoteCommandWindow();
        dialog.FrequentProgramsListBox.ItemsSource = frequentPrograms;
        dialog.ScriptTextBox.Text = initialScript ?? string.Empty;
        return await dialog.ShowDialog<RemoteCommandSubmission?>(owner);
    }

    private async void OkButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var script = ScriptTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(script))
        {
            await ConfirmationDialog.ShowInfoAsync(this, CrossPlatformText.Validation, CrossPlatformText.CommandScriptRequired);
            return;
        }

        Close(new RemoteCommandSubmission(
            script,
            RunAsComboBox.SelectedIndex == 1 ? RemoteCommandRunAs.Administrator : RemoteCommandRunAs.CurrentUser));
    }

    private void CancelButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(null);
    }

    private async void InsertSelectedButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (FrequentProgramsListBox.SelectedItem is not FrequentProgramEntry entry)
        {
            await ConfirmationDialog.ShowInfoAsync(this, CrossPlatformText.Validation, CrossPlatformText.ChooseProgramFirst);
            return;
        }

        var current = ScriptTextBox.Text?.TrimEnd() ?? string.Empty;
        ScriptTextBox.Text = string.IsNullOrWhiteSpace(current)
            ? entry.CommandText
            : $"{current}{Environment.NewLine}{entry.CommandText}";
        RunAsComboBox.SelectedIndex = entry.RunAs == RemoteCommandRunAs.Administrator ? 1 : 0;
    }
}

public sealed record RemoteCommandSubmission(string Script, RemoteCommandRunAs RunAs);
