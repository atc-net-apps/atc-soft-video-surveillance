namespace AtcSoft.VideoPlayer.Wpf;

/// <summary>
/// Bridge for overlay content bindings. The overlay window's DataContext
/// is set to this bridge, exposing the VideoHost's DataContext as
/// <see cref="HostDataContext"/> — same pattern as FlyleafHost.
/// </summary>
internal sealed class OverlayBridge : INotifyPropertyChanged
{
    private object? hostDataContext;

    /// <summary>
    /// Gets or sets the host's DataContext, available for binding
    /// in overlay content via <c>{Binding HostDataContext.xxx}</c>.
    /// </summary>
    public object? HostDataContext
    {
        get => hostDataContext;
        set
        {
            if (ReferenceEquals(hostDataContext, value))
            {
                return;
            }

            hostDataContext = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HostDataContext)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}