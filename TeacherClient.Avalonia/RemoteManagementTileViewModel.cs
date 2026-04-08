using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Teacher.Common.Vnc;

namespace TeacherClient.CrossPlatform;

internal sealed class RemoteManagementTileViewModel : INotifyPropertyChanged, IDisposable
{
    private PinnedPreviewBitmap? _previewBitmap;
    private string _machineName = string.Empty;
    private string _statusText = string.Empty;
    private bool _isSelected;

    public RemoteManagementTileViewModel(DiscoveredAgentRow agent, string initialStatus)
    {
        AgentId = agent.AgentId;
        Agent = agent;
        MachineName = agent.MachineName;
        StatusText = initialStatus;
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

    public DateTimeOffset? LastFailureUtc { get; set; }

    public bool IsDisposed { get; private set; }

    /// <summary>Gets or sets fullscreen <see cref="Dialogs.RemoteVncViewerWindow"/> instances open for this agent (tile preview is paused while &gt; 0).</summary>
    public int FullscreenViewerCount { get; set; }

    public Bitmap? PreviewBitmap => _previewBitmap?.Bitmap;

    internal void SetPreview(PinnedPreviewBitmap? previewBitmap)
    {
        if (ReferenceEquals(_previewBitmap, previewBitmap))
        {
            return;
        }

        _previewBitmap?.Dispose();
        _previewBitmap = previewBitmap;
        OnPropertyChanged(nameof(PreviewBitmap));
    }

    public void Dispose()
    {
        IsDisposed = true;
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
