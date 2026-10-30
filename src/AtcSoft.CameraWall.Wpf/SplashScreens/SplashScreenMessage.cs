namespace AtcSoft.CameraWall.Wpf.SplashScreens;

/// <summary>
/// Message for controlling the splash screen via Messenger.
/// </summary>
public sealed class SplashScreenMessage : MessageBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SplashScreenMessage"/> class
    /// to update the splash screen content.
    /// </summary>
    /// <param name="header">The header text to display.</param>
    /// <param name="version">The version to display.</param>
    public SplashScreenMessage(
        string header,
        Version version)
    {
        ArgumentNullException.ThrowIfNull(header);
        ArgumentNullException.ThrowIfNull(version);

        Header = header;
        Version = version;
        Close = false;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SplashScreenMessage"/> class
    /// to close the splash screen.
    /// </summary>
    /// <param name="close">Must be true to close the splash screen.</param>
    /// <exception cref="ArgumentException">Thrown when close is false.</exception>
    public SplashScreenMessage(bool close)
    {
        if (!close)
        {
            throw new ArgumentException($"{nameof(close)} can only be true", nameof(close));
        }

        Header = string.Empty;
        Version = new Version();
        Close = true;
    }

    /// <summary>
    /// Gets the header text.
    /// </summary>
    public string Header { get; }

    /// <summary>
    /// Gets the version.
    /// </summary>
    public Version Version { get; }

    /// <summary>
    /// Gets a value indicating whether to close the splash screen.
    /// </summary>
    public bool Close { get; }
}