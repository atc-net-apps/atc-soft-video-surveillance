namespace AtcSoft.VideoEngine.Windows.Interop;

/// <summary>
/// One capture-format triple advertised by an MF video source.
/// Width / Height in pixels, FrameRate in Hz, PixelFormat already
/// translated to FFmpeg's spelling via
/// <see cref="MediaFoundation.PixelFormatGuidMapper"/>.
/// </summary>
internal sealed record MfCapability(
    int Width,
    int Height,
    double FrameRate,
    string PixelFormat);