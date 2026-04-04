#nullable enable

using Teacher.Common;
using TeacherClient.Localization;

namespace TeacherClient;

public sealed class ClientUpdateDialog : Form
{
    private readonly TeacherClientUpdateService _service;
    private readonly Label _statusLabel;
    private readonly ProgressBar _progressBar;
    private readonly Label _progressDetailsLabel;
    private readonly TextBox _logTextBox;
    private readonly Label _hintLabel;
    private readonly Button _checkButton;
    private readonly Button _downloadButton;
    private readonly Button _installButton;
    private TeacherClientUpdateCheckResult? _lastCheckResult;
    private TeacherClientInstallerInfo? _installerInfo;
    private string? _lastLoggedMessage;

    public ClientUpdateDialog(TeacherClientUpdateService service)
    {
        _service = service;
        Icon = AppIconLoader.Load();
        Text = TeacherClientText.ClientUpdateTitle;
        MinimumSize = new Size(760, 520);
        Size = new Size(860, 620);
        StartPosition = FormStartPosition.CenterParent;

        _statusLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 28,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point)
        };

        _progressBar = new ProgressBar
        {
            Dock = DockStyle.Top,
            Height = 24,
            Margin = new Padding(0, 0, 0, 8),
            Style = ProgressBarStyle.Continuous
        };

        _progressDetailsLabel = new Label
        {
            Dock = DockStyle.Top,
            Height = 24,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Color.Gainsboro
        };

        _logTextBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            ReadOnly = true,
            WordWrap = true,
            Font = new Font("Consolas", 10F, FontStyle.Regular, GraphicsUnit.Point)
        };

        _hintLabel = new Label
        {
            Dock = DockStyle.Bottom,
            Height = 88,
            TextAlign = ContentAlignment.TopLeft,
            AutoEllipsis = false
        };

        _checkButton = new Button
        {
            Text = TeacherClientText.ClientUpdateCheckButton,
            AutoSize = true,
            MinimumSize = new Size(170, 42)
        };
        _checkButton.Click += async (_, _) => await CheckForUpdatesAsync();

        _downloadButton = new Button
        {
            Text = TeacherClientText.ClientUpdateDownloadButton,
            AutoSize = true,
            MinimumSize = new Size(170, 42),
            Enabled = false
        };
        _downloadButton.Click += async (_, _) => await DownloadUpdateAsync();

        _installButton = new Button
        {
            Text = TeacherClientText.ClientUpdateInstallButton,
            AutoSize = true,
            MinimumSize = new Size(170, 42),
            Enabled = false
        };
        _installButton.Click += (_, _) => InstallUpdate();

        var closeButton = new Button
        {
            Text = TeacherClientText.Close,
            AutoSize = true,
            MinimumSize = new Size(130, 42),
            DialogResult = DialogResult.OK
        };

        var buttonsPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 54,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(0),
            WrapContents = false
        };
        buttonsPanel.Controls.Add(closeButton);
        buttonsPanel.Controls.Add(_installButton);
        buttonsPanel.Controls.Add(_downloadButton);
        buttonsPanel.Controls.Add(_checkButton);

        var contentPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12)
        };
        contentPanel.Controls.Add(_logTextBox);
        contentPanel.Controls.Add(buttonsPanel);
        contentPanel.Controls.Add(_hintLabel);
        contentPanel.Controls.Add(_progressDetailsLabel);
        contentPanel.Controls.Add(_progressBar);
        contentPanel.Controls.Add(_statusLabel);

        Controls.Add(contentPanel);

        _installerInfo = _service.GetReadyInstaller();
        AppendLog(TeacherClientText.ClientUpdateCurrentVersion(_service.CurrentVersion));
        if (_installerInfo is not null)
        {
            AppendLog(TeacherClientText.ClientUpdateReady(_installerInfo.Version));
        }

        _hintLabel.Text = TeacherClientText.ClientUpdateHint(_service.ManifestUrl, _service.DownloadsDirectory);
        _statusLabel.Text = TeacherClientText.ClientUpdateTitle;
        _progressDetailsLabel.Text = string.Empty;
        _installButton.Enabled = _installerInfo is not null;
    }

    private async Task CheckForUpdatesAsync()
    {
        await RunAsync(async progress =>
        {
            _lastCheckResult = await _service.CheckForUpdateAsync(progress);
            AppendLog(_lastCheckResult.UpdateAvailable
                ? TeacherClientText.ClientUpdateAvailable(_lastCheckResult.AvailableVersion, _lastCheckResult.PlatformLabel)
                : TeacherClientText.ClientAlreadyUpToDate(_lastCheckResult.CurrentVersion));
            _downloadButton.Enabled = _lastCheckResult.UpdateAvailable;
        });
    }

    private async Task DownloadUpdateAsync()
    {
        if (_lastCheckResult is null || !_lastCheckResult.UpdateAvailable)
        {
            AppendLog(TeacherClientText.ClientUpdateDownloadMissing);
            return;
        }

        await RunAsync(async progress =>
        {
            _installerInfo = await _service.DownloadInstallerAsync(_lastCheckResult, progress);
            AppendLog(TeacherClientText.ClientUpdateReady(_installerInfo.Version));
            _installButton.Enabled = true;
        });
    }

    private void InstallUpdate()
    {
        if (_installerInfo is null)
        {
            AppendLog(TeacherClientText.ClientUpdateInstallMissing);
            return;
        }

        try
        {
            _service.LaunchInstaller(_installerInfo);
            AppendLog(TeacherClientText.ClientUpdateInstallerOpened(_installerInfo.LocalInstallerPath));
            _statusLabel.Text = TeacherClientText.ClientUpdateInstallStarted;
        }
        catch (Exception ex)
        {
            AppendLog(ex.Message);
            _statusLabel.Text = ex.Message;
        }
    }

    private async Task RunAsync(Func<IProgress<TeacherClientUpdateProgress>, Task> action)
    {
        _checkButton.Enabled = false;
        _downloadButton.Enabled = false;
        _installButton.Enabled = false;
        UseWaitCursor = true;

        var progress = new Progress<TeacherClientUpdateProgress>(UpdateProgress);
        try
        {
            await action(progress);
        }
        catch (Exception ex)
        {
            AppendLog(ex.Message);
            _statusLabel.Text = ex.Message;
        }
        finally
        {
            UseWaitCursor = false;
            _checkButton.Enabled = true;
            _downloadButton.Enabled = _lastCheckResult?.UpdateAvailable == true;
            _installButton.Enabled = _installerInfo is not null;
        }
    }

    private void UpdateProgress(TeacherClientUpdateProgress progress)
    {
        _statusLabel.Text = progress.Message;
        if (progress.Percent is >= 0 and <= 100)
        {
            _progressBar.Style = ProgressBarStyle.Continuous;
            _progressBar.Value = progress.Percent.Value;
        }
        else
        {
            _progressBar.Style = ProgressBarStyle.Marquee;
        }

        _progressDetailsLabel.Text = BuildProgressDetails(progress);
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

        _logTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
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
