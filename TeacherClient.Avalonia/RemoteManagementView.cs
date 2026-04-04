using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Teacher.Common;
using Teacher.Common.Vnc;
using TeacherClient.CrossPlatform.Localization;
using TeacherClient.CrossPlatform.Services;

namespace TeacherClient.CrossPlatform;

public partial class MainWindow
{
    private async void RefreshRemoteManagementButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await RefreshRemoteManagementTilesAsync();
    }

    private async void StartVncViewOnlyButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var agent = GetSelectedRemoteManagementAgent();
        if (agent is null)
        {
            SetStatus(CrossPlatformText.RemoteManagementNoSelection);
            return;
        }

        await StartVncForRemoteManagementAsync(agent, viewOnly: true);
    }

    private async void StartVncControlButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var agent = GetSelectedRemoteManagementAgent();
        if (agent is null)
        {
            SetStatus(CrossPlatformText.RemoteManagementNoSelection);
            return;
        }

        await StartVncForRemoteManagementAsync(agent, viewOnly: false);
    }

    private async void StopVncButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var agent = GetSelectedRemoteManagementAgent();
        if (agent is null)
        {
            SetStatus(CrossPlatformText.RemoteManagementNoSelection);
            return;
        }

        await StopVncForRemoteManagementAsync(agent);
    }

    private async void OpenRemoteManagementViewerButton_OnClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var agent = GetSelectedRemoteManagementAgent();
        if (agent is null)
        {
            SetStatus(CrossPlatformText.RemoteManagementNoSelection);
            return;
        }

        await OpenRemoteManagementViewerAsync(agent);
    }

    private void RemoteManagementListBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (RemoteManagementListBox.SelectedItem is RemoteManagementTileViewModel tile)
        {
            _remoteManagementSelectedAgentId = tile.AgentId;
        }
        else if (RemoteManagementListBox.SelectedItem is null)
        {
            _remoteManagementSelectedAgentId = null;
        }

        UpdateRemoteManagementSelectionVisuals();
    }

    private async void RemoteManagementListBox_OnDoubleTapped(object? sender, Avalonia.Input.TappedEventArgs e)
    {
        var agent = GetSelectedRemoteManagementAgent();
        if (agent is null)
        {
            SetStatus(CrossPlatformText.RemoteManagementNoSelection);
            return;
        }

        await OpenRemoteManagementViewerAsync(agent);
    }

    private async Task RefreshRemoteManagementTilesAsync()
    {
        var onlineAgents = _allAgents
            .Where(x => string.Equals(x.Status, CrossPlatformText.Online, StringComparison.OrdinalIgnoreCase))
            .Where(x => !string.IsNullOrWhiteSpace(x.RespondingAddress))
            .ToList();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            RemoteManagementHintTextBlock.Text = onlineAgents.Count == 0
                ? CrossPlatformText.RemoteManagementNoScreens
                : CrossPlatformText.RemoteManagementHint;

            var keepIds = onlineAgents
                .Select(x => x.AgentId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var stale in _remoteManagementTiles.Where(x => !keepIds.Contains(x.AgentId)).ToList())
            {
                RemoveRemoteManagementTile(stale.AgentId);
            }

            foreach (var agent in onlineAgents)
            {
                var tile = _remoteManagementTiles.FirstOrDefault(x => string.Equals(x.AgentId, agent.AgentId, StringComparison.OrdinalIgnoreCase));
                if (tile is null)
                {
                    tile = CreateRemoteManagementTile(agent);
                    _remoteManagementTiles.Add(tile);
                }
                else
                {
                    UpdateRemoteManagementTile(tile, agent);
                }
            }

            if (!string.IsNullOrWhiteSpace(_remoteManagementSelectedAgentId) && !keepIds.Contains(_remoteManagementSelectedAgentId))
            {
                _remoteManagementSelectedAgentId = null;
            }

            if (string.IsNullOrWhiteSpace(_remoteManagementSelectedAgentId) && onlineAgents.Count > 0)
            {
                _remoteManagementSelectedAgentId = onlineAgents[0].AgentId;
            }

            UpdateRemoteManagementSelectionVisuals();
        });
    }

    private RemoteManagementTileViewModel CreateRemoteManagementTile(DiscoveredAgentRow agent)
    {
        var tile = new RemoteManagementTileViewModel(agent);
        UpdateRemoteManagementTile(tile, agent);
        return tile;
    }

    private void UpdateRemoteManagementTile(RemoteManagementTileViewModel tile, DiscoveredAgentRow agent)
    {
        tile.Agent = agent;
        tile.MachineName = agent.MachineName;
        tile.StatusText = BuildRemoteManagementStatusText(agent);

        if (agent.VncRunning && !string.IsNullOrWhiteSpace(agent.RespondingAddress) && agent.VncPort > 0)
        {
            _ = EnsureRemoteManagementPreviewAsync(tile);
            return;
        }

        StopRemoteManagementPreview(tile);
        tile.SetPreview(CreatePlaceholderBitmap(200, 140));
    }

    private static string BuildRemoteManagementStatusText(DiscoveredAgentRow agent)
    {
        var baseStatus = !string.Equals(agent.Status, CrossPlatformText.Online, StringComparison.OrdinalIgnoreCase)
            ? CrossPlatformText.RemoteManagementStopped(agent.MachineName)
            : !agent.VncEnabled
                ? CrossPlatformText.RemoteManagementDisabled(agent.MachineName)
                : agent.VncRunning
                    ? (agent.VncViewOnly
                        ? CrossPlatformText.RemoteManagementViewOnly(agent.MachineName)
                        : CrossPlatformText.RemoteManagementControl(agent.MachineName))
                    : CrossPlatformText.RemoteManagementStopped(agent.MachineName);

        return string.IsNullOrWhiteSpace(agent.VncStatusMessage)
            ? baseStatus
            : $"{baseStatus} - {agent.VncStatusMessage}";
    }

    private async Task EnsureRemoteManagementPreviewAsync(RemoteManagementTileViewModel tile)
    {
        if (!tile.Agent.VncRunning || string.IsNullOrWhiteSpace(tile.Agent.RespondingAddress) || tile.Agent.VncPort <= 0)
        {
            return;
        }

        var key = $"{tile.Agent.RespondingAddress}:{tile.Agent.VncPort}:{tile.Agent.VncViewOnly}:{_clientSettings.SharedSecret}";
        if (string.Equals(tile.ConnectionKey, key, StringComparison.OrdinalIgnoreCase) &&
            tile.Session?.IsConnected == true &&
            tile.PreviewTask is { IsCompleted: false })
        {
            return;
        }

        StopRemoteManagementPreview(tile);
        tile.ConnectionKey = key;

        var cancellation = new CancellationTokenSource();
        var session = new TeacherVncSession(tile.Agent.RespondingAddress, tile.Agent.VncPort, _clientSettings.SharedSecret);
        tile.PreviewCancellation = cancellation;
        tile.Session = session;
        session.StatusChanged += (_, message) =>
        {
            if (!_isClosing)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    tile.StatusText = $"{BuildRemoteManagementStatusText(tile.Agent)}{(string.IsNullOrWhiteSpace(message) ? string.Empty : $" - {message}")}";
                });
            }
        };
        session.Closed += (_, _) =>
        {
            if (!_isClosing)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (!cancellation.IsCancellationRequested)
                    {
                        tile.StatusText = BuildRemoteManagementStatusText(tile.Agent);
                    }
                });
            }
        };

        tile.PreviewTask = Task.Run(async () =>
        {
            try
            {
                Dispatcher.UIThread.Post(() => tile.StatusText = CrossPlatformText.RemoteManagementConnecting(tile.Agent.MachineName));
                await session.ConnectAsync(cancellation.Token);
                while (!cancellation.Token.IsCancellationRequested)
                {
                    var frame = await session.CaptureFrameAsync(cancellation.Token);
                    if (frame is not null)
                    {
                        var preview = CreatePreviewBitmap(frame);
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            tile.SetPreview(preview);
                            tile.StatusText = BuildRemoteManagementStatusText(tile.Agent);
                        });
                    }

                    await Task.Delay(1500, cancellation.Token);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                if (!_isClosing)
                {
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        tile.StatusText = CrossPlatformText.RemoteManagementConnectionFailed(tile.Agent.MachineName, ex.Message);
                        tile.SetPreview(CreatePlaceholderBitmap(200, 140));
                    });
                }
            }
            finally
            {
                session.Dispose();
                if (ReferenceEquals(tile.Session, session))
                {
                    tile.Session = null;
                }

                if (ReferenceEquals(tile.PreviewCancellation, cancellation))
                {
                    tile.PreviewCancellation = null;
                }

                cancellation.Dispose();
                tile.PreviewTask = null;
            }
        }, cancellation.Token);

        tile.SetPreview(CreatePlaceholderBitmap(200, 140));
    }

    private void StopRemoteManagementPreview(RemoteManagementTileViewModel tile)
    {
        tile.PreviewCancellation?.Cancel();
        tile.Session?.Close();
        tile.Session?.Dispose();
        tile.Session = null;
        tile.PreviewCancellation?.Dispose();
        tile.PreviewCancellation = null;
        tile.PreviewTask = null;
    }

    private async Task StartVncForRemoteManagementAsync(DiscoveredAgentRow agent, bool viewOnly)
    {
        if (!string.Equals(agent.Status, CrossPlatformText.Online, StringComparison.OrdinalIgnoreCase))
        {
            SetStatus(CrossPlatformText.RemoteManagementRequiresOnlineAgent);
            return;
        }

        try
        {
            var client = new TeacherApiClient($"http://{agent.RespondingAddress}:{agent.Port}", _clientSettings.SharedSecret);
            await client.StartVncAsync(
                viewOnly,
                agent.VncPort > 0 ? agent.VncPort : null,
                VncPasswordHelper.Derive(_clientSettings.SharedSecret));
            SetStatus(viewOnly
                ? CrossPlatformText.RemoteManagementRunning(agent.MachineName)
                : CrossPlatformText.RemoteManagementControl(agent.MachineName));
            await LoadAgentsAsync();
            await RefreshRemoteManagementTilesAsync();
        }
        catch (Exception ex)
        {
            SetStatus(CrossPlatformText.RemoteManagementConnectionFailed(agent.MachineName, ex.Message));
        }
    }

    private async Task StopVncForRemoteManagementAsync(DiscoveredAgentRow agent)
    {
        if (!string.Equals(agent.Status, CrossPlatformText.Online, StringComparison.OrdinalIgnoreCase))
        {
            SetStatus(CrossPlatformText.RemoteManagementRequiresOnlineAgent);
            return;
        }

        try
        {
            var client = new TeacherApiClient($"http://{agent.RespondingAddress}:{agent.Port}", _clientSettings.SharedSecret);
            await client.StopVncAsync();
            SetStatus(CrossPlatformText.RemoteManagementStopped(agent.MachineName));
            await LoadAgentsAsync();
            await RefreshRemoteManagementTilesAsync();
        }
        catch (Exception ex)
        {
            SetStatus(CrossPlatformText.RemoteManagementConnectionFailed(agent.MachineName, ex.Message));
        }
    }

    private async Task OpenRemoteManagementViewerAsync(DiscoveredAgentRow agent)
    {
        if (!agent.VncRunning || string.IsNullOrWhiteSpace(agent.RespondingAddress) || agent.VncPort <= 0)
        {
            SetStatus(CrossPlatformText.RemoteManagementStopped(agent.MachineName));
            return;
        }

        var viewer = new Dialogs.RemoteVncViewerWindow(
            agent.MachineName,
            agent.RespondingAddress,
            agent.VncPort,
            _clientSettings.SharedSecret,
            controlEnabled: !agent.VncViewOnly);
        viewer.Show(this);
        await Task.CompletedTask;
    }

    private DiscoveredAgentRow? GetSelectedRemoteManagementAgent()
    {
        var selected = _remoteManagementTiles.FirstOrDefault(x => x.IsSelected);
        if (selected is not null)
        {
            return selected.Agent;
        }

        if (string.IsNullOrWhiteSpace(_remoteManagementSelectedAgentId))
        {
            return null;
        }

        return _remoteManagementTiles.FirstOrDefault(x => string.Equals(x.AgentId, _remoteManagementSelectedAgentId, StringComparison.OrdinalIgnoreCase))?.Agent;
    }

    private void UpdateRemoteManagementSelectionVisuals()
    {
        foreach (var tile in _remoteManagementTiles)
        {
            tile.IsSelected = string.Equals(tile.AgentId, _remoteManagementSelectedAgentId, StringComparison.OrdinalIgnoreCase);
        }
    }

    private void DisposeRemoteManagementTiles()
    {
        foreach (var tile in _remoteManagementTiles.ToList())
        {
            StopRemoteManagementPreview(tile);
            tile.Dispose();
        }

        _remoteManagementTiles.Clear();
    }

    private void RemoveRemoteManagementTile(string agentId)
    {
        var tile = _remoteManagementTiles.FirstOrDefault(x => string.Equals(x.AgentId, agentId, StringComparison.OrdinalIgnoreCase));
        if (tile is null)
        {
            return;
        }

        StopRemoteManagementPreview(tile);
        tile.Dispose();
        _remoteManagementTiles.Remove(tile);
    }

    private static PinnedPreviewBitmap CreatePreviewBitmap(VncFrameCapture frame)
        => PinnedPreviewBitmap.Create(frame.Pixels, frame.Width, frame.Height, frame.Stride);

    private static PinnedPreviewBitmap CreatePlaceholderBitmap(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (var index = 0; index < pixels.Length; index += 4)
        {
            pixels[index] = 36;
            pixels[index + 1] = 29;
            pixels[index + 2] = 24;
            pixels[index + 3] = 255;
        }

        return PinnedPreviewBitmap.Create(pixels, width, height, width * 4);
    }

    private sealed class RemoteManagementTileViewModel : INotifyPropertyChanged, IDisposable
    {
        private PinnedPreviewBitmap? _previewBitmap;
        private string _machineName = string.Empty;
        private string _statusText = string.Empty;
        private bool _isSelected;

        public RemoteManagementTileViewModel(DiscoveredAgentRow agent)
        {
            AgentId = agent.AgentId;
            Agent = agent;
            MachineName = agent.MachineName;
            StatusText = BuildRemoteManagementStatusText(agent);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public string AgentId { get; }
        public DiscoveredAgentRow Agent { get; set; }

        public string MachineName
        {
            get => _machineName;
            set => SetField(ref _machineName, value);
        }

        public string StatusText
        {
            get => _statusText;
            set => SetField(ref _statusText, value);
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (SetField(ref _isSelected, value))
                {
                    OnPropertyChanged(nameof(SelectionBrush));
                }
            }
        }

        public IBrush SelectionBrush => IsSelected ? Brushes.DodgerBlue : Brushes.Transparent;

        public string? ConnectionKey { get; set; }
        public TeacherVncSession? Session { get; set; }
        public CancellationTokenSource? PreviewCancellation { get; set; }
        public Task? PreviewTask { get; set; }

        public void SetPreview(PinnedPreviewBitmap? previewBitmap)
        {
            if (ReferenceEquals(_previewBitmap, previewBitmap))
            {
                return;
            }

            _previewBitmap?.Dispose();
            _previewBitmap = previewBitmap;
            OnPropertyChanged(nameof(PreviewBitmap));
        }

        public Bitmap? PreviewBitmap => _previewBitmap?.Bitmap;

        public void Dispose()
        {
            _previewBitmap?.Dispose();
            _previewBitmap = null;
            Session?.Dispose();
            Session = null;
            PreviewCancellation?.Dispose();
            PreviewCancellation = null;
        }

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return false;
            }

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    private sealed class PinnedPreviewBitmap : IDisposable
    {
        private readonly GCHandle _handle;

        private PinnedPreviewBitmap(Bitmap bitmap, GCHandle handle)
        {
            Bitmap = bitmap;
            _handle = handle;
        }

        public Bitmap Bitmap { get; }

        public static PinnedPreviewBitmap Create(byte[] pixels, int width, int height, int stride)
        {
            var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try
            {
                var bitmap = new Bitmap(
                    PixelFormat.Bgra8888,
                    AlphaFormat.Unpremul,
                    handle.AddrOfPinnedObject(),
                    new PixelSize(width, height),
                    new Vector(96, 96),
                    stride);

                return new PinnedPreviewBitmap(bitmap, handle);
            }
            catch
            {
                handle.Free();
                throw;
            }
        }

        public void Dispose()
        {
            Bitmap.Dispose();
            if (_handle.IsAllocated)
            {
                _handle.Free();
            }
        }
    }
}
