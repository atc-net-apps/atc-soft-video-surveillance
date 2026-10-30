namespace AtcSoft.VideoSurveillance.Helpers;

/// <summary>
/// Translates WinRT <c>MediaEncodingSubtypes</c> strings (uppercase
/// FOURCC-style names such as <c>NV12</c>, <c>YUY2</c>, <c>MJPG</c>)
/// to the FFmpeg pixel-format spellings the rest of the stack carries
/// (and that dshow's <c>pixel_format</c> option understands —
/// <c>nv12</c>, <c>yuyv422</c>, etc.).
/// </summary>
/// <remarks>
/// Mirrors the GUID-keyed table in
/// <c>AtcSoft.VideoEngine.Windows.MediaFoundation.PixelFormatGuidMapper</c>
/// but takes the picker-friendly string form. Kept in
/// <c>AtcSoft.VideoSurveillance.Core</c> so both the Wpf.Core
/// dialog code-path and the helper that builds FFmpeg locators can
/// reach it without referencing the Windows-specific MF assembly.
/// </remarks>
public static class MediaSubtypeMapper
{
    /// <summary>
    /// Returns the FFmpeg-friendly pixel-format string for the given
    /// WinRT subtype, or the input unchanged when no mapping exists —
    /// callers fall back to passing the value through verbatim so a
    /// new device-reported subtype isn't silently dropped.
    /// </summary>
    public static string MapToFFmpeg(string? subtype)
    {
        if (string.IsNullOrEmpty(subtype))
        {
            return string.Empty;
        }

        // Case-insensitive match — WinRT publishes uppercase FOURCCs,
        // but some drivers (and the v4l2 enumerator) yield lowercase
        // or mixed-case spellings.
        return subtype.ToUpperInvariant() switch
        {
            "NV12" => "nv12",
            "YUY2" or "YUYV" => "yuyv422",
            "UYVY" => "uyvy422",
            "I420" or "IYUV" => "yuv420p",
            "YV12" => "yuv420p",
            "RGB24" => "rgb24",
            "RGB32" => "bgra",
            "ARGB32" => "argb",
            "MJPG" or "MJPEG" => "mjpeg",
            "H264" => "h264",
            "H265" or "HEVC" => "hevc",
            _ => subtype,
        };
    }
}