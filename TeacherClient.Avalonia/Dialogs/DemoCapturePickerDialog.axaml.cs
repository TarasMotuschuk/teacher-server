using Avalonia.Controls;
using Avalonia.Interactivity;
using TeacherClient.CrossPlatform.Localization;
using TeacherClient.CrossPlatform.Models;
using TeacherClient.CrossPlatform.Services;

namespace TeacherClient.CrossPlatform.Dialogs;

public partial class DemoCapturePickerDialog : Window
{
    private readonly DemoWindowEnumerationService _enumerator = new();
    private List<DemoWindowInfo> _windows = [];

    public DemoCapturePickerDialog()
    {
        InitializeComponent();
        Title = CrossPlatformText.DemonstrationSourceDialogTitle;
        PromptTextBlock.Text = CrossPlatformText.DemonstrationSourcePrompt;
        ScreenRadio.Content = CrossPlatformText.DemonstrationSourceScreenOption;
        WindowRadio.Content = CrossPlatformText.DemonstrationSourceWindowOption;
        WindowListLabel.Text = CrossPlatformText.DemonstrationSourceWindowListLabel;
        OkButton.Content = CrossPlatformText.DemonstrationSourceStart;
        CancelButton.Content = CrossPlatformText.Cancel;
        WindowsListBox.SelectionChanged += (_, _) => UpdateUiState();
        UpdateUiState();
        _ = LoadWindowsAsync();
    }

    public static async Task<DemoCaptureTarget?> ShowAsync(Window owner)
    {
        var dlg = new DemoCapturePickerDialog();
        return await dlg.ShowDialog<DemoCaptureTarget?>(owner);
    }

    private async Task LoadWindowsAsync()
    {
        try
        {
            StatusTextBlock.Text = CrossPlatformText.DemonstrationSourceLoadingWindows;
            await Task.Yield();
            _windows = _enumerator.GetTopLevelWindows();
            WindowsListBox.ItemsSource = _windows.Select(w => w.Title).ToList();
            StatusTextBlock.Text = _windows.Count == 0
                ? CrossPlatformText.DemonstrationSourceNoWindowsFound
                : CrossPlatformText.DemonstrationSourceWindowsFound(_windows.Count);
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = CrossPlatformText.DemonstrationSourceEnumerateFailed(ex.Message);
        }
        finally
        {
            UpdateUiState();
        }
    }

    private void ModeRadio_OnChecked(object? sender, RoutedEventArgs e)
    {
        UpdateUiState();
    }

    private void UpdateUiState()
    {
        var windowMode = WindowRadio.IsChecked == true;
        WindowsListBox.IsEnabled = windowMode;
        WindowListLabel.Opacity = windowMode ? 1.0 : 0.6;

        if (!windowMode)
        {
            OkButton.IsEnabled = true;
            return;
        }

        OkButton.IsEnabled = WindowsListBox.SelectedIndex >= 0;
    }

    private void OkButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (ScreenRadio.IsChecked == true)
        {
            Close(new DemoCaptureTarget(DemoCaptureTargetKind.Screen, 0, 0, 0, 0));
            return;
        }

        var idx = WindowsListBox.SelectedIndex;
        if (idx < 0 || idx >= _windows.Count)
        {
            return;
        }

        var w = _windows[idx];
        Close(new DemoCaptureTarget(
            DemoCaptureTargetKind.Window,
            0,
            0,
            0,
            0,
            PlatformWindowId: w.PlatformWindowId,
            WindowTitle: w.Title));
    }

    private void CancelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }
}

