namespace AtcSoft.VideoEngine;

/// <summary>
/// Selects the FFmpeg input-format for <see cref="StreamOptions"/>.
/// <see cref="Auto"/> preserves the legacy network behaviour where
/// FFmpeg sniffs the URL scheme; the explicit values force a local
/// device demuxer that requires
/// <see cref="StreamOptions.RawDeviceSpec"/>.
/// </summary>
public enum InputFormatKind
{
    /// <summary>
    /// FFmpeg auto-detects the input format from the URL scheme
    /// (rtsp://, http://, file://, etc.). The historical network path.
    /// </summary>
    Auto = 0,

    /// <summary>
    /// Windows DirectShow input — used for USB / UVC cameras and other
    /// directly-attached capture devices.
    /// </summary>
    Dshow = 1,

    /// <summary>
    /// Linux Video4Linux2 input. Reserved for the deferred Linux phase.
    /// </summary>
    V4l2 = 2,

    /// <summary>
    /// macOS AVFoundation input. Reserved for the deferred macOS phase.
    /// </summary>
    AVFoundation = 3,
}