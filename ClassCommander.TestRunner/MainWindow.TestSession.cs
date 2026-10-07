using System.Security.Cryptography;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using ClassCommander.TestRunner.Localization;
using ClassCommander.TestRunner.Services;
using Teacher.Common.Contracts.Testing;

namespace ClassCommander.TestRunner;

public partial class MainWindow
{
    private readonly DispatcherTimer _sessionTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    private TestSessionKeyboardGuard? _keyboardGuard;
    private bool _sessionProtected;
    private bool _sessionDialogOpen;
    private Window? _activeSessionDialog;
    private bool _exitRequested;

    private void StartTestSession()
    {
        if (RunnerLaunchOptions.Current.ExitCodeHash is null)
        {
            return;
        }

        _keyboardGuard = new TestSessionKeyboardGuard();
        _sessionProtected = true;
        SessionBanner.IsVisible = true;
        SessionNoticeText.Text = TestRunnerText.SessionNotice;
        ExitTestButton.Content = TestRunnerText.ExitTest;
        CanResize = false;
        Topmost = true;
        WindowState = WindowState.FullScreen;
        _sessionTimer.Tick += MaintainTestWindow;
        _sessionTimer.Start();
    }

    private void MaintainTestWindow(object? sender, EventArgs e)
    {
        if (!_sessionProtected)
        {
            return;
        }

        if (_sessionDialogOpen)
        {
            if (_activeSessionDialog is { IsVisible: true, IsActive: false })
            {
                _activeSessionDialog.Activate();
            }

            return;
        }

        if (WindowState != WindowState.FullScreen)
        {
            WindowState = WindowState.FullScreen;
        }

        if (!IsActive)
        {
            Activate();
        }
    }

    private void ReleaseTestSession()
    {
        _sessionProtected = false;
        _sessionTimer.Stop();
        _sessionTimer.Tick -= MaintainTestWindow;
        _keyboardGuard?.Dispose();
        _keyboardGuard = null;
        SessionBanner.IsVisible = false;
        Topmost = false;
        CanResize = true;
        if (WindowState == WindowState.FullScreen)
        {
            WindowState = WindowState.Normal;
        }
    }

    private async void ExitTestButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        => await RequestTeacherExitAsync();

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (_sessionProtected && e.CloseReason != WindowCloseReason.OSShutdown)
        {
            e.Cancel = true;
            Dispatcher.UIThread.Post(() => _ = RequestTeacherExitAsync());
        }

        base.OnClosing(e);
    }

    private async Task RequestTeacherExitAsync()
    {
        if (!_sessionProtected || _busy || _exitRequested || _sessionDialogOpen)
        {
            return;
        }

        _exitRequested = true;
        try
        {
            var code = new TextBox { PasswordChar = '●', MaxLength = 6, MinHeight = 40 };
            var error = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = Brushes.DarkRed };
            var cancel = new Button { Content = TestRunnerText.StayInTest };
            var exit = new Button { Content = TestRunnerText.ExitTest };
            var dialog = new Window
            {
                Title = TestRunnerText.ExitTest,
                Width = 520,
                SizeToContent = SizeToContent.Height,
                CanResize = false,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new StackPanel
                {
                    Margin = new Thickness(20),
                    Spacing = 12,
                    Children =
                    {
                        new TextBlock { Text = TestRunnerText.TeacherCodePrompt, TextWrapping = TextWrapping.Wrap },
                        code,
                        error,
                        new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { cancel, exit } },
                    },
                },
            };
            cancel.Click += (_, _) => dialog.Close(false);
            exit.Click += (_, _) =>
            {
                var hash = SHA256.HashData(Encoding.UTF8.GetBytes(code.Text ?? string.Empty));
                var expected = Convert.FromHexString(RunnerLaunchOptions.Current.ExitCodeHash!);
                if (CryptographicOperations.FixedTimeEquals(hash, expected))
                {
                    dialog.Close(true);
                }
                else
                {
                    error.Text = TestRunnerText.WrongTeacherCode;
                    code.Clear();
                }
            };
            dialog.Opened += (_, _) => code.Focus();
            if (!await ShowSessionDialogAsync<bool>(dialog))
            {
                return;
            }

            _busy = true;
            StatusTextBlock.Text = TestRunnerText.Connecting;
            try
            {
                CaptureCurrentAnswer();
                if (_api is not null && _attempt is not null)
                {
                    await _api.SaveProgressAsync(_attempt.AttemptPublicId, _attempt.AttemptToken,
                        new SaveAttemptProgressRequest(BuildAnswers(isFinal: false), BuildClientProgress(), ReplaceAnswers: true));
                }
            }
            catch
            {
                if (!await ConfirmAsync(TestRunnerText.ExitSaveFailed))
                {
                    return;
                }
            }
            finally
            {
                _busy = false;
            }

            ReleaseTestSession();
            Close();
        }
        finally
        {
            _exitRequested = false;
        }
    }

    private async Task<T> ShowSessionDialogAsync<T>(Window dialog)
    {
        _sessionDialogOpen = true;
        _activeSessionDialog = dialog;
        dialog.Topmost = _sessionProtected;
        try
        {
            return await dialog.ShowDialog<T>(this);
        }
        finally
        {
            _sessionDialogOpen = false;
            _activeSessionDialog = null;
        }
    }
}
