namespace AtcSoft.VideoSurveillance.Wpf.Core.Windows;

/// <summary>
/// A tile's player on a <see cref="MediaElement"/>, muted: several play at once.
/// </summary>
public sealed class MediaElementPlayAllMedia : IPlayAllMedia
{
    private readonly MediaElement element;
    private TimeSpan pendingPosition;

    /// <summary>
    /// Initializes a new instance of the <see cref="MediaElementPlayAllMedia"/> class.
    /// </summary>
    /// <param name="element">The tile's media element, with a manual loaded behaviour.</param>
    public MediaElementPlayAllMedia(MediaElement element)
    {
        ArgumentNullException.ThrowIfNull(element);

        this.element = element;
        element.IsMuted = true;
        element.MediaOpened += (_, _) =>
        {
            element.Position = pendingPosition;
            Opened?.Invoke(this, EventArgs.Empty);
        };
        element.MediaEnded += (_, _) => Ended?.Invoke(this, EventArgs.Empty);
        element.MediaFailed += (_, _) => Failed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public event EventHandler? Opened;

    /// <inheritdoc />
    public event EventHandler? Ended;

    /// <inheritdoc />
    public event EventHandler? Failed;

    /// <inheritdoc />
    public TimeSpan Position => element.Position;

    /// <inheritdoc />
    public void Open(
        Uri address,
        TimeSpan position)
    {
        pendingPosition = position;
        element.Source = address;
        element.Play();
    }

    /// <inheritdoc />
    public void Play()
        => element.Play();

    /// <inheritdoc />
    public void Pause()
        => element.Pause();

    /// <inheritdoc />
    public void Close()
    {
        element.Stop();
        element.Close();
        element.Source = null;
    }
}