using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Teacher.Common;
using TeacherClient.CrossPlatform.Localization;

namespace TeacherClient.CrossPlatform.Dialogs;

public sealed class UpdatePreparationWindow : Window
{
    private readonly TeacherUpdatePreparationService _service;
    private readonly TextBlock _statusTextBlock;
    private readonly ProgressBar _progressBar;
    private readonly TextBlock _progressDetailsTextBlock;
    private readonly TextBox _logTextBox;
    private readonly TextBlock _hintTextBlock;
    private readonly Button _checkButton;
    private readonly Button _downloadButton;
    private TeacherUpdateCheckResult? _lastCheckResult;
    private string? _lastLoggedMessage;

    public UpdatePreparationWindow(TeacherUpdatePreparationService service)
    {
        _service = service;
        Icon = AppIconLoader.Load();
        Title = CrossPlatformText.UpdatePreparationTitle;
        Width = 860;
        Height = 620;
        MinWidth = 720;
        MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _statusTextBlock = new TextBlock
        {
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
            Margin = new Thickness(0, 0, 0, 8),
        };

        _progressBar = new ProgressBar
        {
            Height = 20,
            Minimum = 0,
            Maximum = 100,
            Margin = new Thickness(0, 0, 0, 10),
        };

        _progressDetailsTextBlock = new TextBlock
        {
            Margin = new Thickness(0, 0, 0, 10),
            Foreground = Avalonia.Media.Brushes.LightGray,
        };

        _logTextBox = new TextBox
        {
            AcceptsReturn = true,
            IsReadOnly = true,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };

        _hintTextBlock = new TextBlock
        {
            Margin = new Thickness(0, 10, 0, 10),
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Foreground = Avalonia.Media.Brushes.LightGray,
        };

        _checkButton = new Button
        {
            Content = CrossPlatformText.UpdatePreparationCheckButton,
            MinWidth = 170,
        };
        _checkButton.Click += async (_, _) => await CheckForUpdatesAsync();

        _downloadButton = new Button
        {
            Content = CrossPlatformText.UpdatePreparationDownloadButton,
            MinWidth = 170,
            IsEnabled = false,
        };
        _downloadButton.Click += async (_, _) => await DownloadUpdateAsync();

        var closeButton = new Button
        {
            Content = CrossPlatformText.Close,
            MinWidth = 130,
        };
        closeButton.Click += (_, _) => Close();

        var buttonsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        buttonsPanel.Children.Add(_checkButton);
        buttonsPanel.Children.Add(_downloadButton);
        buttonsPanel.Children.Add(closeButton);

        var rootGrid = new Grid
        {
            Margin = new Thickness(16),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*,Auto,Auto"),
        };
        rootGrid.Children.Add(_statusTextBlock);
        rootGrid.Children.Add(_progressBar);
        rootGrid.Children.Add(_progressDetailsTextBlock);
        rootGrid.Children.Add(_logTextBox);
        rootGrid.Children.Add(_hintTextBlock);
        rootGrid.Children.Add(buttonsPanel);
        Grid.SetRow(_progressBar, 1);
        Grid.SetRow(_progressDetailsTextBlock, 2);
        Grid.SetRow(_logTextBox, 3);
        Grid.SetRow(_hintTextBlock, 4);
        Grid.SetRow(buttonsPanel, 5);

        Content = rootGrid;

        AppendLog(_service.GetPreparedUpdate() is { } prepared
            ? CrossPlatformText.UpdatePreparationReady(prepared.Version)
            : CrossPlatformText.UpdatePreparationMissing);
        _hintTextBlock.Text = CrossPlatformText.UpdatePreparationManualHint(_service.ManifestUrl, _service.ManualDirectory);
        _statusTextBlock.Text = CrossPlatformText.UpdatePreparationTitle;
        _progressDetailsTextBlock.Text = string.Empty;
    }

    private async Task CheckForUpdatesAsync()
    {
        await RunAsync(async progress =>
        {
            _lastCheckResult = await _service.CheckForUpdateAsync(progress);
            _downloadButton.IsEnabled = true;
        });
    }

    private async Task DownloadUpdateAsync()
    {
        if (_lastCheckResult is null)
        {
            AppendLog(CrossPlatformText.UpdatePreparationMissing);
            return;
        }

        await RunAsync(async progress =>
        {
            var prepared = await _service.DownloadOrPrepareAsync(_lastCheckResult, progress);
            AppendLog(CrossPlatformText.UpdatePreparationReady(prepared.Version));
        });
    }

    private async Task RunAsync(Func<IProgress<TeacherUpdatePreparationProgress>, Task> action)
    {
        _checkButton.IsEnabled = false;
        _downloadButton.IsEnabled = false;
        var progress = new Progress<TeacherUpdatePreparationProgress>(UpdateProgress);

        try
        {
            await action(progress);
        }
        catch (Exception ex)
        {
            _statusTextBlock.Text = ex.Message;
            AppendLog(ex.Message);
        }
        finally
        {
            _checkButton.IsEnabled = true;
            _downloadButton.IsEnabled = _lastCheckResult is not null;
        }
    }

    private void UpdateProgress(TeacherUpdatePreparationProgress progress)
    {
        _statusTextBlock.Text = progress.Message;
        if (progress.Percent is >= 0 and <= 100)
        {
            _progressBar.IsIndeterminate = false;
            _progressBar.Value = progress.Percent.Value;
        }
        else
        {
            _progressBar.IsIndeterminate = true;
        }

        _progressDetailsTextBlock.Text = UpdatePreparationWindowTextFormatter.BuildProgressDetails(progress);
        AppendMeaningfulLog(progress);
    }

    private void AppendMeaningfulLog(TeacherUpdatePreparationProgress progress)
    {
        var message = UpdatePreparationWindowTextFormatter.BuildLogMessage(progress);
        if (string.Equals(_lastLoggedMessage, message, StringComparison.Ordinal))
        {
            return;
        }

        _lastLoggedMessage = message;
        AppendLog(message);
    }

    private void AppendLog(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        _logTextBox.Text = string.Concat(_logTextBox.Text, $"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
        _logTextBox.CaretIndex = _logTextBox.Text?.Length ?? 0;
    }
}
