using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using TeacherClient.CrossPlatform.Localization;
using TeacherClient.CrossPlatform.Models;

namespace TeacherClient.CrossPlatform.Dialogs;

public partial class ManualAgentWindow : Window
{
    public ManualAgentWindow()
    {
        InitializeComponent();
        ApplyLocalization();
    }

    public ManualAgentWindow(ManualAgentEntry? entry)
        : this()
    {
        if (entry is null)
        {
            return;
        }

        DisplayNameTextBox.Text = entry.DisplayName;
        IpAddressTextBox.Text = entry.IpAddress;
        PortNumericUpDown.Value = entry.Port;
        GroupNameTextBox.Text = entry.GroupName;
        MacAddressTextBox.Text = entry.MacAddress;
        NotesTextBox.Text = entry.Notes;
    }

    public ManualAgentEntry ToEntry(string? existingId = null)
    {
        return new ManualAgentEntry
        {
            Id = existingId ?? Guid.NewGuid().ToString("N"),
            DisplayName = DisplayNameTextBox.Text?.Trim() ?? string.Empty,
            IpAddress = IpAddressTextBox.Text?.Trim() ?? string.Empty,
            Port = decimal.ToInt32(PortNumericUpDown.Value ?? 5055),
            GroupName = GroupNameTextBox.Text?.Trim() ?? string.Empty,
            MacAddress = MacAddressTextBox.Text?.Trim() ?? string.Empty,
            Notes = NotesTextBox.Text?.Trim() ?? string.Empty,
        };
    }

    private void SaveButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(DisplayNameTextBox.Text))
        {
            ShowValidation(CrossPlatformText.DisplayNameRequired);
            return;
        }

        if (string.IsNullOrWhiteSpace(IpAddressTextBox.Text))
        {
            ShowValidation(CrossPlatformText.IpAddressRequired);
            return;
        }

        Close(true);
    }

    private void CancelButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(false);
    }

    private void ApplyLocalization()
    {
        Title = CrossPlatformText.ManualAgentTitle;
        DisplayNameLabel.Text = CrossPlatformText.DisplayName;
        IpAddressLabel.Text = CrossPlatformText.IpAddress;
        GroupLabel.Text = CrossPlatformText.Group;
        MacAddressLabel.Text = CrossPlatformText.MacAddress;
        NotesLabel.Text = CrossPlatformText.Notes;
        SaveButton.Content = CrossPlatformText.Save;
        CancelButton.Content = CrossPlatformText.Cancel;
    }

    private void ShowValidation(string message)
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow is not null)
        {
            _ = ConfirmationDialog.ShowInfoAsync(desktop.MainWindow, CrossPlatformText.Validation, message);
        }
    }
}
