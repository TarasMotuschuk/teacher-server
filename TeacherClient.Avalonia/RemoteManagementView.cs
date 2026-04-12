using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
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
        e.Handled = true;
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

    /// <summary>
    /// Opens the fullscreen viewer on double-click. The handler is on the tile border (not the list) so the tile
    /// view-model is always correct. ListBox-level DoubleTapped often had <c>e.Source</c> at the list, so resolving
    /// the item failed. Uses <c>ClickCount == 2</c> when available, plus a time-based second click within 600 ms.
    /// </summary>
    private async void RemoteManagementTileRoot_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border { DataContext: RemoteManagementTileViewModel tile })
        {
            return;
        }

        if (!e.GetCurrentPoint((Visual)sender).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var manualDouble = string.Equals(_remoteTileLastPrimaryAgentId, tile.AgentId, StringComparison.OrdinalIgnoreCase)
            && (now - _remoteTileLastPrimaryUtc).TotalMilliseconds <= 600;
        _remoteTileLastPrimaryUtc = now;
        _remoteTileLastPrimaryAgentId = tile.AgentId;

        if (e.ClickCount != 2 && !manualDouble)
        {
            return;
        }

        e.Handled = true;
        _remoteTileLastPrimaryAgentId = null;

        _remoteManagementSelectedAgentId = tile.AgentId;
        RemoteManagementListBox.SelectedItem = tile;
        UpdateRemoteManagementSelectionVisuals();

        await OpenRemoteManagementViewerAsync(tile.Agent);
    }

    private void CloseAllRemoteVncViewerWindows()
    {
        List<Dialogs.RemoteVncViewerWindow> copy;
        lock (_openRemoteVncViewersSync)
        {
            copy = _openRemoteVncViewers.Values.ToList();
            _openRemoteVncViewers.Clear();
        }

        foreach (var w in copy)
        {
            try
            {
                w.Close();
            }
            catch
            {
            }
        }
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
        var tile = new RemoteManagementTileViewModel(
            agent,
            RemoteManagementViewHelpers.BuildRemoteManagementStatusText(
                agent.Status,
                agent.MachineName,
                agent.VncEnabled,
                agent.VncRunning,
                agent.VncViewOnly,
                agent.VncStatusMessage));
        UpdateRemoteManagementTile(tile, agent);
        return tile;
    }

    private void UpdateRemoteManagementTile(RemoteManagementTileViewModel tile, DiscoveredAgentRow agent)
    {
        tile.Agent = agent;
        tile.MachineName = agent.MachineName;
        tile.StatusText = RemoteManagementViewHelpers.BuildRemoteManagementStatusText(
            agent.Status,
            agent.MachineName,
            agent.VncEnabled,
            agent.VncRunning,
            agent.VncViewOnly,
            agent.VncStatusMessage);

        if (agent.VncRunning && !string.IsNullOrWhiteSpace(agent.RespondingAddress) && agent.VncPort > 0)
        {
            _ = EnsureRemoteManagementPreviewAsync(tile);
            return;
        }

        RemoteManagementViewHelpers.StopRemoteManagementPreviewNoWait(tile);
        tile.SetPreview(RemoteManagementViewHelpers.CreatePlaceholderBitmap(200, 140));
    }

    private async Task EnsureRemoteManagementPreviewAsync(RemoteManagementTileViewModel tile)
    {
        if (!tile.Agent.VncRunning || string.IsNullOrWhiteSpace(tile.Agent.RespondingAddress) || tile.Agent.VncPort <= 0)
        {
            return;
        }

        // Do not start tile preview while a fullscreen viewer is open for this agent: preview would open a second
        // VNC client and races teardown when the tile session is stopped/refreshed.
        if (tile.FullscreenViewerCount > 0)
        {
            return;
        }

        if (tile.LastFailureUtc is { } lastFailure &&
            DateTimeOffset.UtcNow - lastFailure < TimeSpan.FromSeconds(5))
        {
            return;
        }

        var key = $"{tile.Agent.RespondingAddress}:{tile.Agent.VncPort}:False:{_clientSettings.SharedSecret}";
        if (string.Equals(tile.ConnectionKey, key, StringComparison.OrdinalIgnoreCase) &&
            tile.Session?.IsConnected == true &&
            tile.PreviewTask is { IsCompleted: false })
        {
            return;
        }

        await RemoteManagementViewHelpers.StopRemoteManagementPreviewAsync(tile);
        tile.ConnectionKey = key;

        var cancellation = new CancellationTokenSource();
        var session = new TeacherVncSession(
            tile.Agent.RespondingAddress,
            tile.Agent.VncPort,
            _clientSettings.SharedSecret,
            controlEnabled: false);
        tile.PreviewCancellation = cancellation;
        tile.Session = session;
        session.StatusChanged += (_, message) =>
        {
            if (!_isClosing && !tile.IsDisposed)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (!tile.IsDisposed)
                    {
                        tile.StatusText = $"{RemoteManagementViewHelpers.BuildRemoteManagementStatusText(
                            tile.Agent.Status,
                            tile.Agent.MachineName,
                            tile.Agent.VncEnabled,
                            tile.Agent.VncRunning,
                            tile.Agent.VncViewOnly,
                            tile.Agent.VncStatusMessage)}{(string.IsNullOrWhiteSpace(message) ? string.Empty : $" - {message}")}";
                    }
                });
            }
        };
        session.Closed += (_, _) =>
        {
            if (!_isClosing && !tile.IsDisposed)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (!cancellation.IsCancellationRequested && !tile.IsDisposed)
                    {
                        tile.StatusText = RemoteManagementViewHelpers.BuildRemoteManagementStatusText(
                            tile.Agent.Status,
                            tile.Agent.MachineName,
                            tile.Agent.VncEnabled,
                            tile.Agent.VncRunning,
                            tile.Agent.VncViewOnly,
                            tile.Agent.VncStatusMessage);
                    }
                });
            }
        };

        tile.PreviewTask = Task.Run(
            async () =>
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
                        tile.LastFailureUtc = null;
                        var preview = RemoteManagementViewHelpers.CreatePreviewBitmap(frame);
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            tile.SetPreview(preview);
                            tile.StatusText = RemoteManagementViewHelpers.BuildRemoteManagementStatusText(
                                tile.Agent.Status,
                                tile.Agent.MachineName,
                                tile.Agent.VncEnabled,
                                tile.Agent.VncRunning,
                                tile.Agent.VncViewOnly,
                                tile.Agent.VncStatusMessage);
                        });
                    }

                    await Task.Delay(2500, cancellation.Token);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                if (!_isClosing && !tile.IsDisposed)
                {
                    tile.LastFailureUtc = DateTimeOffset.UtcNow;
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (!tile.IsDisposed)
                        {
                            tile.StatusText = CrossPlatformText.RemoteManagementConnectionFailed(tile.Agent.MachineName, ex.Message);
                            tile.SetPreview(RemoteManagementViewHelpers.CreatePlaceholderBitmap(200, 140));
                        }
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

        tile.SetPreview(RemoteManagementViewHelpers.CreatePlaceholderBitmap(200, 140));
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

        lock (_openRemoteVncViewersSync)
        {
            if (_openRemoteVncViewers.TryGetValue(agent.AgentId, out var existing) && existing.IsVisible)
            {
                existing.Activate();
                if (existing.WindowState == WindowState.Minimized)
                {
                    existing.WindowState = WindowState.Maximized;
                }

                return;
            }

            if (_openRemoteVncViewers.ContainsKey(agent.AgentId))
            {
                _openRemoteVncViewers.Remove(agent.AgentId);
            }
        }

        var tile = _remoteManagementTiles.FirstOrDefault(x => string.Equals(x.AgentId, agent.AgentId, StringComparison.OrdinalIgnoreCase));
        if (tile is not null)
        {
            // Always stop the tile preview before opening the viewer. Sharing one TeacherVncSession between the
            // preview loop and RemoteVncViewerWindow caused use-after-dispose when the preview was stopped or
            // refreshed while the viewer was still open.
            await RemoteManagementViewHelpers.StopRemoteManagementPreviewAsync(tile);
        }

        // Another OpenRemoteManagementViewerAsync can finish while we awaited StopRemoteManagementPreviewAsync; re-check
        // before creating a second window for the same agent.
        lock (_openRemoteVncViewersSync)
        {
            if (_openRemoteVncViewers.TryGetValue(agent.AgentId, out var existingAfterAwait) && existingAfterAwait.IsVisible)
            {
                existingAfterAwait.Activate();
                if (existingAfterAwait.WindowState == WindowState.Minimized)
                {
                    existingAfterAwait.WindowState = WindowState.Maximized;
                }

                return;
            }
        }

        if (tile is not null)
        {
            tile.FullscreenViewerCount++;
        }

        var viewer = new Dialogs.RemoteVncViewerWindow(
            agent.MachineName,
            agent.RespondingAddress,
            agent.VncPort,
            _clientSettings.SharedSecret,
            controlEnabled: false);

        lock (_openRemoteVncViewersSync)
        {
            _openRemoteVncViewers[agent.AgentId] = viewer;
        }

        viewer.Closed += async (_, _) =>
        {
            lock (_openRemoteVncViewersSync)
            {
                if (_openRemoteVncViewers.TryGetValue(agent.AgentId, out var w) && ReferenceEquals(w, viewer))
                {
                    _openRemoteVncViewers.Remove(agent.AgentId);
                }
            }

            if (tile is not null)
            {
                tile.FullscreenViewerCount = Math.Max(0, tile.FullscreenViewerCount - 1);
            }

            if (_isClosing || !agent.VncRunning || tile is null)
            {
                return;
            }

            try
            {
                await EnsureRemoteManagementPreviewAsync(tile);
            }
            catch
            {
            }
        };

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                viewer.Show();
                viewer.Activate();
            }
            else
            {
                viewer.Show(this);
            }
        }
        catch
        {
            lock (_openRemoteVncViewersSync)
            {
                if (_openRemoteVncViewers.TryGetValue(agent.AgentId, out var w) && ReferenceEquals(w, viewer))
                {
                    _openRemoteVncViewers.Remove(agent.AgentId);
                }
            }

            if (tile is not null)
            {
                tile.FullscreenViewerCount = Math.Max(0, tile.FullscreenViewerCount - 1);
            }

            throw;
        }
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
            RemoteManagementViewHelpers.StopRemoteManagementPreviewNoWait(tile);
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

        RemoteManagementViewHelpers.StopRemoteManagementPreviewNoWait(tile);
        tile.Dispose();
        _remoteManagementTiles.Remove(tile);
    }
}
