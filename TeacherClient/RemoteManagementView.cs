#nullable enable

using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Teacher.Common;
using Teacher.Common.Vnc;
using TeacherClient.Localization;
using TeacherClient.Services;

namespace TeacherClient;

public partial class MainForm
{
    private async void refreshRemoteManagementButton_Click(object? sender, EventArgs e)
    {
        await RefreshRemoteManagementCardsAsync();
    }

    private async void startRemoteManagementViewOnlyButton_Click(object? sender, EventArgs e)
    {
        var agent = GetSelectedRemoteManagementAgent();
        if (agent is null)
        {
            SetStatus(TeacherClientText.RemoteManagementNoSelection);
            return;
        }

        await StartVncForRemoteManagementAsync(agent, viewOnly: true);
    }

    private async void startRemoteManagementControlButton_Click(object? sender, EventArgs e)
    {
        var agent = GetSelectedRemoteManagementAgent();
        if (agent is null)
        {
            SetStatus(TeacherClientText.RemoteManagementNoSelection);
            return;
        }

        await StartVncForRemoteManagementAsync(agent, viewOnly: false);
    }

    private async void stopRemoteManagementButton_Click(object? sender, EventArgs e)
    {
        var agent = GetSelectedRemoteManagementAgent();
        if (agent is null)
        {
            SetStatus(TeacherClientText.RemoteManagementNoSelection);
            return;
        }

        await StopVncForRemoteManagementAsync(agent);
    }

    private async void openRemoteManagementViewerButton_Click(object? sender, EventArgs e)
    {
        var agent = GetSelectedRemoteManagementAgent();
        if (agent is null)
        {
            SetStatus(TeacherClientText.RemoteManagementNoSelection);
            return;
        }

        await OpenRemoteManagementViewerAsync(agent);
    }

    private async Task RefreshRemoteManagementCardsAsync()
    {
        var onlineAgents = _allAgents
            .Where(x => string.Equals(x.Status, TeacherClientText.Online, StringComparison.OrdinalIgnoreCase))
            .Where(x => !string.IsNullOrWhiteSpace(x.RespondingAddress))
            .ToList();

        remoteManagementHintLabel.Text = onlineAgents.Count == 0
            ? TeacherClientText.RemoteManagementNoScreens
            : TeacherClientText.RemoteManagementHint;

        var keepIds = onlineAgents
            .Select(x => x.AgentId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var stale in _remoteManagementCards.Keys.Where(id => !keepIds.Contains(id)).ToList())
        {
            RemoveRemoteManagementCard(stale);
        }

        foreach (var agent in onlineAgents)
        {
            if (!_remoteManagementCards.TryGetValue(agent.AgentId, out var card))
            {
                card = CreateRemoteManagementCard(agent);
                _remoteManagementCards[agent.AgentId] = card;
                remoteManagementCardsPanel.Controls.Add(card.Container);
            }

            UpdateRemoteManagementCard(card, agent);
        }

        if (!string.IsNullOrWhiteSpace(_remoteManagementSelectedAgentId) &&
            !keepIds.Contains(_remoteManagementSelectedAgentId))
        {
            _remoteManagementSelectedAgentId = null;
        }

        if (string.IsNullOrWhiteSpace(_remoteManagementSelectedAgentId) && onlineAgents.Count > 0)
        {
            SelectRemoteManagementCard(onlineAgents[0].AgentId);
        }
        else
        {
            UpdateRemoteManagementSelectionVisuals();
        }
    }

    private RemoteManagementCardState CreateRemoteManagementCard(DiscoveredAgentRow agent)
    {
        var outer = new Panel
        {
            Width = 238,
            Height = 286,
            Margin = new Padding(0, 0, 12, 12),
            Padding = new Padding(2),
            BackColor = Color.FromArgb(191, 199, 208)
        };

        var inner = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(10)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 154F));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32F));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

        var preview = new PictureBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(24, 29, 36),
            SizeMode = PictureBoxSizeMode.StretchImage
        };

        var title = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 10.5F, FontStyle.Bold, GraphicsUnit.Point),
            AutoEllipsis = true
        };

        var status = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft,
            ForeColor = Color.FromArgb(75, 85, 99),
            AutoEllipsis = false
        };

        layout.Controls.Add(preview, 0, 0);
        layout.Controls.Add(title, 0, 1);
        layout.Controls.Add(status, 0, 2);
        inner.Controls.Add(layout);
        outer.Controls.Add(inner);

        AttachRemoteManagementCardHandlers(outer, agent.AgentId);
        AttachRemoteManagementCardHandlers(inner, agent.AgentId);
        AttachRemoteManagementCardHandlers(preview, agent.AgentId);
        AttachRemoteManagementCardHandlers(title, agent.AgentId);
        AttachRemoteManagementCardHandlers(status, agent.AgentId);

        return new RemoteManagementCardState(agent.AgentId, agent, outer, preview, title, status);
    }

    private static string BuildRemoteManagementStatusText(DiscoveredAgentRow agent)
    {
        var baseStatus = !string.Equals(agent.Status, TeacherClientText.Online, StringComparison.OrdinalIgnoreCase)
            ? TeacherClientText.RemoteManagementStopped(agent.MachineName)
            : !agent.VncEnabled
                ? TeacherClientText.RemoteManagementDisabled(agent.MachineName)
                : agent.VncRunning
                    ? (agent.VncViewOnly
                        ? TeacherClientText.RemoteManagementViewOnly(agent.MachineName)
                        : TeacherClientText.RemoteManagementControl(agent.MachineName))
                    : TeacherClientText.RemoteManagementStopped(agent.MachineName);

        return string.IsNullOrWhiteSpace(agent.VncStatusMessage)
            ? baseStatus
            : $"{baseStatus} - {agent.VncStatusMessage}";
    }

    private void SetRemoteManagementPreview(RemoteManagementCardState card, Bitmap? bitmap)
    {
        if (card.Container.IsDisposed || card.PreviewPictureBox.IsDisposed)
        {
            bitmap?.Dispose();
            return;
        }

        if (card.PreviewPictureBox.InvokeRequired)
        {
            card.PreviewPictureBox.BeginInvoke(new Action(() => SetRemoteManagementPreview(card, bitmap)));
            return;
        }

        var oldPreview = card.CurrentPreview;
        card.CurrentPreview = bitmap;
        card.PreviewPictureBox.Image = bitmap;
        if (!ReferenceEquals(oldPreview, bitmap))
        {
            oldPreview?.Dispose();
        }
    }

    private void DisposeRemoteManagementCards()
    {
        foreach (var card in _remoteManagementCards.Values.ToList())
        {
            StopRemoteManagementPreview(card);
            card.Dispose();
        }

        _remoteManagementCards.Clear();
    }

    private void UpdateRemoteManagementCard(RemoteManagementCardState card, DiscoveredAgentRow agent)
    {
        card.Agent = agent;
        card.TitleLabel.Text = $"{agent.MachineName}";
        card.StatusLabel.Text = BuildRemoteManagementStatusText(agent);
        if (agent.VncRunning && !string.IsNullOrWhiteSpace(agent.RespondingAddress) && agent.VncPort > 0)
        {
            _ = EnsureRemoteManagementPreviewAsync(card);
            return;
        }

        StopRemoteManagementPreview(card);
        SetRemoteManagementPreview(card, CreateRemoteManagementPlaceholderBitmap(agent, card.StatusLabel.Text));
    }

    private async Task EnsureRemoteManagementPreviewAsync(RemoteManagementCardState card)
    {
        if (!card.Agent.VncRunning || string.IsNullOrWhiteSpace(card.Agent.RespondingAddress) || card.Agent.VncPort <= 0)
        {
            return;
        }

        if (card.LastFailureUtc is { } lastFailure &&
            DateTimeOffset.UtcNow - lastFailure < TimeSpan.FromSeconds(5))
        {
            return;
        }

        var key = $"{card.Agent.RespondingAddress}:{card.Agent.VncPort}:{card.Agent.VncViewOnly}:{_clientSettings.SharedSecret}";
        if (string.Equals(card.ConnectionKey, key, StringComparison.OrdinalIgnoreCase) &&
            card.Session?.IsConnected == true &&
            card.PreviewTask is { IsCompleted: false })
        {
            return;
        }

        StopRemoteManagementPreview(card);
        card.ConnectionKey = key;

        var cancellation = new CancellationTokenSource();
        var session = new TeacherVncSession(
            card.Agent.RespondingAddress,
            card.Agent.VncPort,
            _clientSettings.SharedSecret,
            controlEnabled: !card.Agent.VncViewOnly);
        card.PreviewCancellation = cancellation;
        card.Session = session;
        session.StatusChanged += (_, message) =>
        {
            if (!IsDisposed)
            {
                BeginInvoke(new Action(() => card.StatusLabel.Text = $"{BuildRemoteManagementStatusText(card.Agent)}{(string.IsNullOrWhiteSpace(message) ? string.Empty : $" - {message}")}"));
            }
        };
        session.Closed += (_, _) =>
        {
            if (!IsDisposed)
            {
                BeginInvoke(new Action(() =>
                {
                    if (!cancellation.IsCancellationRequested)
                    {
                        card.StatusLabel.Text = BuildRemoteManagementStatusText(card.Agent);
                    }
                }));
            }
        };

        card.PreviewTask = Task.Run(async () =>
        {
            try
            {
                card.StatusLabel.BeginInvoke(new Action(() => card.StatusLabel.Text = TeacherClientText.RemoteManagementConnecting(card.Agent.MachineName)));
                await session.ConnectAsync(cancellation.Token);
                while (!cancellation.Token.IsCancellationRequested)
                {
                    var frame = await session.CaptureFrameAsync(cancellation.Token);
                    if (frame is not null)
                    {
                        card.LastFailureUtc = null;
                        var bitmap = CreateThumbnailBitmap(frame, 200);
                        SetRemoteManagementPreview(card, bitmap);
                        card.StatusLabel.BeginInvoke(new Action(() => card.StatusLabel.Text = BuildRemoteManagementStatusText(card.Agent)));
                    }

                    await Task.Delay(2500, cancellation.Token);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                if (!IsDisposed)
                {
                    card.LastFailureUtc = DateTimeOffset.UtcNow;
                    BeginInvoke(new Action(() =>
                    {
                        card.StatusLabel.Text = TeacherClientText.RemoteManagementConnectionFailed(card.Agent.MachineName, ex.Message);
                        SetRemoteManagementPreview(card, CreateRemoteManagementPlaceholderBitmap(card.Agent, card.StatusLabel.Text));
                    }));
                }
            }
            finally
            {
                session.Dispose();
                if (ReferenceEquals(card.Session, session))
                {
                    card.Session = null;
                }

                if (ReferenceEquals(card.PreviewCancellation, cancellation))
                {
                    card.PreviewCancellation = null;
                }

                cancellation.Dispose();
                card.PreviewTask = null;
            }
        }, cancellation.Token);

        SetRemoteManagementPreview(card, CreateRemoteManagementPlaceholderBitmap(card.Agent, TeacherClientText.RemoteManagementConnecting(card.Agent.MachineName)));
    }

    private void StopRemoteManagementPreview(RemoteManagementCardState card)
    {
        card.PreviewCancellation?.Cancel();
        card.Session?.Close();
        card.Session?.Dispose();
        card.Session = null;
        card.PreviewCancellation?.Dispose();
        card.PreviewCancellation = null;
        card.PreviewTask = null;
    }

    private async Task StartVncForRemoteManagementAsync(DiscoveredAgentRow agent, bool viewOnly)
    {
        if (!string.Equals(agent.Status, TeacherClientText.Online, StringComparison.OrdinalIgnoreCase))
        {
            SetStatus(TeacherClientText.RemoteManagementRequiresOnlineAgent);
            return;
        }

        try
        {
            using var cursorScope = new CursorScope(this);
            var client = new TeacherApiClient($"http://{agent.RespondingAddress}:{agent.Port}", _clientSettings.SharedSecret);
            await client.StartVncAsync(
                viewOnly,
                agent.VncPort > 0 ? agent.VncPort : null,
                VncPasswordHelper.Derive(_clientSettings.SharedSecret));
            SetStatus(viewOnly
                ? TeacherClientText.RemoteManagementRunning(agent.MachineName)
                : TeacherClientText.RemoteManagementControl(agent.MachineName));
            await LoadDiscoveredAgentsAsync(false);
            await RefreshRemoteManagementCardsAsync();
        }
        catch (Exception ex)
        {
            SetStatus($"{TeacherClientText.RemoteManagementConnectionFailed(agent.MachineName, ex.Message)}");
        }
    }

    private async Task StopVncForRemoteManagementAsync(DiscoveredAgentRow agent)
    {
        if (!string.Equals(agent.Status, TeacherClientText.Online, StringComparison.OrdinalIgnoreCase))
        {
            SetStatus(TeacherClientText.RemoteManagementRequiresOnlineAgent);
            return;
        }

        try
        {
            using var cursorScope = new CursorScope(this);
            var client = new TeacherApiClient($"http://{agent.RespondingAddress}:{agent.Port}", _clientSettings.SharedSecret);
            await client.StopVncAsync();
            SetStatus(TeacherClientText.RemoteManagementStopped(agent.MachineName));
            await LoadDiscoveredAgentsAsync(false);
            await RefreshRemoteManagementCardsAsync();
        }
        catch (Exception ex)
        {
            SetStatus($"{TeacherClientText.RemoteManagementConnectionFailed(agent.MachineName, ex.Message)}");
        }
    }

    private async Task OpenRemoteManagementViewerAsync(DiscoveredAgentRow agent)
    {
        if (!agent.VncRunning || string.IsNullOrWhiteSpace(agent.RespondingAddress) || agent.VncPort <= 0)
        {
            SetStatus(TeacherClientText.RemoteManagementStopped(agent.MachineName));
            return;
        }

        RemoteManagementCardState? card = null;
        if (_remoteManagementCards.TryGetValue(agent.AgentId, out var existingCard))
        {
            card = existingCard;
            StopRemoteManagementPreview(card);
        }

        var viewer = new RemoteVncViewerForm(
            agent.MachineName,
            agent.RespondingAddress,
            agent.VncPort,
            _clientSettings.SharedSecret,
            controlEnabled: !agent.VncViewOnly);

        if (card is not null)
        {
            viewer.FormClosed += async (_, _) =>
            {
                if (IsDisposed || !agent.VncRunning)
                {
                    return;
                }

                try
                {
                    await EnsureRemoteManagementPreviewAsync(card);
                }
                catch
                {
                }
            };
        }

        viewer.Show(this);
        await Task.CompletedTask;
    }

    private DiscoveredAgentRow? GetSelectedRemoteManagementAgent()
    {
        if (string.IsNullOrWhiteSpace(_remoteManagementSelectedAgentId))
        {
            return null;
        }

        return _remoteManagementCards.TryGetValue(_remoteManagementSelectedAgentId, out var card) ? card.Agent : null;
    }

    private void SelectRemoteManagementCard(string agentId)
    {
        _remoteManagementSelectedAgentId = agentId;
        UpdateRemoteManagementSelectionVisuals();
    }

    private void UpdateRemoteManagementSelectionVisuals()
    {
        foreach (var card in _remoteManagementCards.Values)
        {
            var selected = string.Equals(card.AgentId, _remoteManagementSelectedAgentId, StringComparison.OrdinalIgnoreCase);
            card.Selected = selected;
            card.Container.BackColor = selected
                ? Color.FromArgb(59, 130, 246)
                : Color.FromArgb(191, 199, 208);
            card.Container.Padding = selected ? new Padding(3) : new Padding(2);
        }
    }

    private void RemoveRemoteManagementCard(string agentId)
    {
        if (!_remoteManagementCards.TryGetValue(agentId, out var card))
        {
            return;
        }

        StopRemoteManagementPreview(card);
        remoteManagementCardsPanel.Controls.Remove(card.Container);
        card.Dispose();
        _remoteManagementCards.Remove(agentId);
    }

    private void AttachRemoteManagementCardHandlers(Control control, string agentId)
    {
        control.Click += (_, _) => SelectRemoteManagementCard(agentId);
        control.DoubleClick += async (_, _) =>
        {
            SelectRemoteManagementCard(agentId);
            if (_remoteManagementCards.TryGetValue(agentId, out var card))
            {
                await OpenRemoteManagementViewerAsync(card.Agent);
            }
        };
    }

    private sealed class RemoteManagementCardState(string agentId, DiscoveredAgentRow agent, Panel container, PictureBox previewPictureBox, Label titleLabel, Label statusLabel) : IDisposable
    {
        public string AgentId { get; } = agentId;
        public DiscoveredAgentRow Agent { get; set; } = agent;
        public Panel Container { get; } = container;
        public PictureBox PreviewPictureBox { get; } = previewPictureBox;
        public Label TitleLabel { get; } = titleLabel;
        public Label StatusLabel { get; } = statusLabel;
        public TeacherVncSession? Session { get; set; }
        public CancellationTokenSource? PreviewCancellation { get; set; }
        public Task? PreviewTask { get; set; }
        public string? ConnectionKey { get; set; }
        public DateTimeOffset? LastFailureUtc { get; set; }
        public bool Selected { get; set; }
        public Bitmap? CurrentPreview { get; set; }

        public void Dispose()
        {
            CurrentPreview?.Dispose();
            CurrentPreview = null;
            PreviewPictureBox.Image = null;
            Session?.Dispose();
            Session = null;
            PreviewCancellation?.Dispose();
            PreviewCancellation = null;
        }
    }

    private static Bitmap CreateThumbnailBitmap(VncFrameCapture frame, int targetWidth)
    {
        var sourceWidth = Math.Max(1, frame.Width);
        var sourceHeight = Math.Max(1, frame.Height);
        var width = Math.Max(1, targetWidth);
        var height = Math.Max(1, (int)Math.Round(sourceHeight * (width / (double)sourceWidth)));
        var thumbnail = new Bitmap(width, height, PixelFormat.Format32bppArgb);

        var source = new Bitmap(sourceWidth, sourceHeight, PixelFormat.Format32bppArgb);
        var sourceData = source.LockBits(new Rectangle(0, 0, sourceWidth, sourceHeight), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            var converted = ConvertRgbaToBgra(frame);
            Marshal.Copy(converted, 0, sourceData.Scan0, Math.Min(converted.Length, Math.Abs(sourceData.Stride) * sourceHeight));
        }
        finally
        {
            source.UnlockBits(sourceData);
        }

        using (var graphics = Graphics.FromImage(thumbnail))
        {
            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            graphics.DrawImage(source, new Rectangle(0, 0, width, height));
        }

        source.Dispose();
        return thumbnail;
    }

    private static byte[] ConvertRgbaToBgra(VncFrameCapture frame)
    {
        var converted = new byte[frame.Width * frame.Height * 4];

        for (var y = 0; y < frame.Height; y++)
        {
            var sourceRow = y * frame.Stride;
            var targetRow = y * frame.Width * 4;

            for (var x = 0; x < frame.Width; x++)
            {
                var sourceIndex = sourceRow + (x * 4);
                var targetIndex = targetRow + (x * 4);

                converted[targetIndex] = frame.Pixels[sourceIndex + 2];
                converted[targetIndex + 1] = frame.Pixels[sourceIndex + 1];
                converted[targetIndex + 2] = frame.Pixels[sourceIndex];
                converted[targetIndex + 3] = 255;
            }
        }

        return converted;
    }

    private static Bitmap CreateRemoteManagementPlaceholderBitmap(DiscoveredAgentRow agent, string status)
    {
        const int width = 200;
        const int height = 140;
        var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        graphics.Clear(Color.FromArgb(24, 29, 36));

        using var titleBrush = new SolidBrush(Color.White);
        using var statusBrush = new SolidBrush(Color.FromArgb(156, 163, 175));
        using var titleFont = new Font("Segoe UI", 10F, FontStyle.Bold, GraphicsUnit.Point);
        using var statusFont = new Font("Segoe UI", 8.5F, FontStyle.Regular, GraphicsUnit.Point);
        var titleRect = new RectangleF(10, 14, width - 20, 40);
        var statusRect = new RectangleF(10, 60, width - 20, height - 70);
        graphics.DrawString(agent.MachineName, titleFont, titleBrush, titleRect);
        graphics.DrawString(status, statusFont, statusBrush, statusRect);
        return bitmap;
    }

}
