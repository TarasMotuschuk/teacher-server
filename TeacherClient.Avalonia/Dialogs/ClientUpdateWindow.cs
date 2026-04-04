using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Teacher.Common;
using TeacherClient.CrossPlatform.Localization;

namespace TeacherClient.CrossPlatform.Dialogs;

public sealed class ClientUpdateWindow : Window
{
    private readonly TeacherClientUpdateService _service;
    private readonly TextBlock _statusTextBlock;
    private readonly ProgressBar _progressBar;
    private readonly TextBlock _progressDetailsTextBlock;
    private readonly TextBox _logTextBox;
    private readonly TextBlock _hintTextBlock;
    private readonly Button _checkButton;
    private readonly Button _downloadButton;
    private readonly Button _installButton;
    private TeacherClientUpdateCheckResult? _lastCheckResult;
    private TeacherClientInstallerInfo? _installerInfo;
    private string? _lastLoggedMessage;

    public ClientUpdateWindow(TeacherClientUpdateService service)
    {
        _service = service;
        Icon = AppIconLoader.Load();
        Title = CrossPlatformText.ClientUpdateTitle;
        Width = 860;
        Height = 620;
        MinWidth = 720;
        MinHeight = 480;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        _statusTextBlock = new TextBlock
        {
            FontWeight = Avalonia.Media.FontWeight.SemiBold,
            Margin = new Thickness(0, 0, 0, 8)
        };

        _progressBar = new ProgressBar
        {
            Height = 20,
            Minimum = 0,
            Maximum = 100,
            Margin = new Thickness(0, 0, 0, 10)
        };

        _progressDetailsTextBlock = new TextBlock
        {
            Margin = new Thickness(0, 0, 0, 10),
            Foreground = Avalonia.Media.Brushes.LightGray
        };

        _logTextBox = new TextBox
        {
            AcceptsReturn = true,
            IsReadOnly = true,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch
        };

        _hintTextBlock = new TextBlock
        {
            Margin = new Thickness(0, 10, 0, 10),
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            Foreground = Avalonia.Media.Brushes.LightGray
        };

        _checkButton = new Button
        {
            Content = CrossPlatformText.ClientUpdateCheckButton,
            MinWidth = 170
        };
        _checkButton.Click += async (_, _) => await CheckForUpdatesAsync();

        _downloadButton = new Button
        {
            Content = CrossPlatformText.ClientUpdateDownloadButton,
            MinWidth = 170,
            IsEnabled = false
        };
        _downloadButton.Click += async (_, _) => await DownloadUpdateAsync();

        _installButton = new Button
        {
            Content = CrossPlatformText.ClientUpdateInstallButton,
            MinWidth = 170,
            IsEnabled = false
        };
        _installButton.Click += (_, _) => InstallUpdate();

        var closeButton = new Button
        {
            Content = CrossPlatformText.Close,
            MinWidth = 130
        };
        closeButton.Click += (_, _) => Close();

        var buttonsPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 10,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        buttonsPanel.Children.Add(_checkButton);
        buttonsPanel.Children.Add(_downloadButton);
        buttonsPanel.Children.Add(_installButton);
        buttonsPanel.Children.Add(closeButton);

        var rootGrid = new Grid
        {
            Margin = new Thickness(16),
            RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*,Auto,Auto")
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

        _installerInfo = _service.GetReadyInstaller();
        AppendLog(CrossPlatformText.ClientUpdateCurrentVersion(_service.CurrentVersion));
        if (_installerInfo is not null)
        {
            AppendLog(CrossPlatformText.ClientUpdateReady(_installerInfo.Version));
        }

        _hintTextBlock.Text = CrossPlatformText.ClientUpdateHint(_service.ManifestUrl, _service.DownloadsDirectory);
        _statusTextBlock.Text = CrossPlatformText.ClientUpdateTitle;
        _progressDetailsTextBlock.Text = string.Empty;
        _installButton.IsEnabled = _installerInfo is not null;
    }

    private async Task CheckForUpdatesAsync()
    {
        await RunAsync(async progress =>
        {
            _lastCheckResult = await _service.CheckForUpdateAsync(progress);
            AppendLog(_lastCheckResult.UpdateAvailable
                ? CrossPlatformText.ClientUpdateAvailable(_lastCheckResult.AvailableVersion, _lastCheckResult.PlatformLabel)
                : CrossPlatformText.ClientAlreadyUpToDate(_lastCheckResult.CurrentVersion));
            _downloadButton.IsEnabled = _lastCheckResult.UpdateAvailable;
        });
    }

    private async Task DownloadUpdateAsync()
    {
        if (_lastCheckResult is null || !_lastCheckResult.UpdateAvailable)
        {
            AppendLog(CrossPlatformText.ClientUpdateDownloadMissing);
            return;
        }

        await RunAsync(async progress =>
        {
            _installerInfo = await _service.DownloadInstallerAsync(_lastCheckResult, progress);
            AppendLog(CrossPlatformText.ClientUpdateReady(_installerInfo.Version));
            _installButton.IsEnabled = true;
        });
    }

    private void InstallUpdate()
    {
        if (_installerInfo is null)
        {
            AppendLog(CrossPlatformText.ClientUpdateInstallMissing);
            return;
        }

        try
        {
            _service.LaunchInstaller(_installerInfo);
            AppendLog(CrossPlatformText.ClientUpdateInstallerOpened(_installerInfo.LocalInstallerPath));
            _statusTextBlock.Text = CrossPlatformText.ClientUpdateInstallStarted;
        }
        catch (Exception ex)
        {
            _statusTextBlock.Text = ex.Message;
            AppendLog(ex.Message);
        }
    }

    private async Task RunAsync(Func<IProgress<TeacherClientUpdateProgress>, Task> action)
    {
        _checkButton.IsEnabled = false;
        _downloadButton.IsEnabled = false;
        _installButton.IsEnabled = false;
        var progress = new Progress<TeacherClientUpdateProgress>(UpdateProgress);

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
            _downloadButton.IsEnabled = _lastCheckResult?.UpdateAvailable == true;
            _installButton.IsEnabled = _installerInfo is not null;
        }
    }

    private void UpdateProgress(TeacherClientUpdateProgress progress)
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

        _progressDetailsTextBlock.Text = BuildProgressDetails(progress);
        AppendMeaningfulLog(progress);
    }

    private void AppendMeaningfulLog(TeacherClientUpdateProgress progress)
    {
        var message = BuildLogMessage(progress);
        if (string.Equals(_lastLoggedMessage, message, StringComparison.Ordinal))
        {
            return;
        }

        _lastLoggedMessage = message;
        AppendLog(message);
    }

    private string BuildLogMessage(TeacherClientUpdateProgress progress)
    {
        var details = BuildProgressDetails(progress);
        return string.IsNullOrWhiteSpace(details) ? progress.Message : $"{progress.Message} {details}";
    }

    private static string BuildProgressDetails(TeacherClientUpdateProgress progress)
    {
        if (progress.TotalBytes.HasValue && progress.BytesTransferred.HasValue)
        {
            var percent = progress.Percent is >= 0 and <= 100 ? $" ({progress.Percent.Value}%)" : string.Empty;
            return $"{FormatByteSize(progress.BytesTransferred.Value)} / {FormatByteSize(progress.TotalBytes.Value)}{percent}";
        }

        return progress.Percent is >= 0 and <= 100
            ? $"{progress.Percent.Value}%"
            : string.Empty;
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

    private static string FormatByteSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = bytes;
        var unitIndex = 0;

        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return unitIndex == 0 ? $"{value:0} {units[unitIndex]}" : $"{value:0.##} {units[unitIndex]}";
    }
}
