namespace AtcSoft.VideoSurveillance.Helpers;

/// <summary>
/// Bundle of everything FFmpeg needs to open a single camera source.
/// Replaces the network-only "just a URL" abstraction by also carrying
/// the input-format selector and per-device options used by USB / DShow
/// / V4L2 sources.
/// </summary>
/// <remarks>
/// <para>
/// For a network camera, <see cref="Uri"/> is the rtsp/http/https URL
/// and the rest are <see langword="null"/>.
/// </para>
/// <para>
/// For a USB camera, <see cref="Uri"/> is a <c>dshow:</c>-scheme
/// placeholder (so logging keeps working) but FFmpeg does not parse it
/// — the demuxer reads <see cref="RawDeviceSpec"/>
/// (e.g. <c>video=Logitech BRIO</c>) and <see cref="InputFormat"/>
/// (e.g. <c>dshow</c>) and feeds them straight to
/// <c>avformat_open_input</c>.
/// </para>
/// </remarks>
public sealed class SourceLocator
{
    public SourceLocator(
        Uri uri,
        string? inputFormat = null,
        string? rawDeviceSpec = null,
        string? videoSize = null,
        string? frameRate = null,
        string? pixelFormat = null,
        bool includesAudio = false)
    {
        ArgumentNullException.ThrowIfNull(uri);
        Uri = uri;
        InputFormat = string.IsNullOrEmpty(inputFormat) ? null : inputFormat;
        RawDeviceSpec = string.IsNullOrEmpty(rawDeviceSpec) ? null : rawDeviceSpec;
        VideoSize = string.IsNullOrEmpty(videoSize) ? null : videoSize;
        FrameRate = string.IsNullOrEmpty(frameRate) ? null : frameRate;
        PixelFormat = string.IsNullOrEmpty(pixelFormat) ? null : pixelFormat;
        IncludesAudio = includesAudio;
    }

    /// <summary>
    /// Always populated. For network sources this is the RTSP/HTTP URL
    /// FFmpeg will open. For local sources this is a synthetic
    /// <c>dshow:</c> / <c>v4l2:</c> placeholder used for logging only.
    /// </summary>
    public Uri Uri { get; }

    /// <summary>
    /// FFmpeg input-format name (<c>dshow</c>, <c>v4l2</c>,
    /// <c>avfoundation</c>) or <see langword="null"/> for auto-detection
    /// (the legacy network behaviour).
    /// </summary>
    public string? InputFormat { get; }

    /// <summary>
    /// Raw device specifier passed verbatim to <c>avformat_open_input</c>
    /// when <see cref="InputFormat"/> is non-null. For dshow this looks
    /// like <c>video=Logitech BRIO</c> or
    /// <c>video=A:audio=B</c>.
    /// </summary>
    public string? RawDeviceSpec { get; }

    /// <summary>FFmpeg <c>video_size</c> option (e.g. <c>1920x1080</c>).</summary>
    public string? VideoSize { get; }

    /// <summary>FFmpeg <c>framerate</c> option (e.g. <c>30</c>, <c>29.97</c>).</summary>
    public string? FrameRate { get; }

    /// <summary>FFmpeg <c>pixel_format</c> option (e.g. <c>nv12</c>).</summary>
    public string? PixelFormat { get; }

    /// <summary>
    /// <see langword="true"/> when the source was asked to deliver sound
    /// as well (a USB camera with its microphone). A recording of it is
    /// then made with sound.
    /// </summary>
    public bool IncludesAudio { get; }

    /// <summary>
    /// <see langword="true"/> when the locator references a local
    /// device (<see cref="InputFormat"/> is non-null) rather than a
    /// network URL. Convenience for branching code that doesn't want
    /// to compare strings.
    /// </summary>
    public bool IsLocalDevice => InputFormat is not null;
}