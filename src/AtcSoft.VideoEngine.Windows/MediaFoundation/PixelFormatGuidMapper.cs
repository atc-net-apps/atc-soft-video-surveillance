namespace AtcSoft.VideoEngine.Windows.MediaFoundation;

/// <summary>
/// Maps Media Foundation <c>MF_MT_SUBTYPE</c> GUIDs to the FFmpeg
/// pixel-format strings the rest of the stack carries (and that
/// dshow's <c>pixel_format</c> option understands). Returns
/// <see langword="null"/> for unknown / non-video GUIDs so callers
/// can skip them rather than push gibberish into <c>StreamOptions</c>.
/// </summary>
/// <remarks>
/// MF subtype GUIDs follow the pattern
/// <c>XXXXXXXX-0000-0010-8000-00aa00389b71</c> where the first four
/// bytes are a FOURCC (e.g. <c>'NV12'</c> = <c>0x3231564E</c>). For
/// well-known types like <c>NV12</c>, <c>YUY2</c>, <c>MJPG</c>, MF
/// publishes named GUID constants (<c>MFVideoFormat_NV12</c>, etc.).
/// We pin them as <see cref="Guid"/> values rather than depending on
/// an external interop package.
/// </remarks>
[SuppressMessage("Style", "SA1310:Field names should not contain underscore", Justification = "Mirrors Win32 GUID names")]
internal static class PixelFormatGuidMapper
{
    public static readonly Guid MFVideoFormat_NV12 = new("3231564E-0000-0010-8000-00AA00389B71");
    public static readonly Guid MFVideoFormat_YUY2 = new("32595559-0000-0010-8000-00AA00389B71");
    public static readonly Guid MFVideoFormat_UYVY = new("59565955-0000-0010-8000-00AA00389B71");
    public static readonly Guid MFVideoFormat_RGB24 = new("00000014-0000-0010-8000-00AA00389B71");
    public static readonly Guid MFVideoFormat_RGB32 = new("00000016-0000-0010-8000-00AA00389B71");
    public static readonly Guid MFVideoFormat_ARGB32 = new("00000015-0000-0010-8000-00AA00389B71");
    public static readonly Guid MFVideoFormat_I420 = new("30323449-0000-0010-8000-00AA00389B71");
    public static readonly Guid MFVideoFormat_IYUV = new("56555949-0000-0010-8000-00AA00389B71");
    public static readonly Guid MFVideoFormat_YV12 = new("32315659-0000-0010-8000-00AA00389B71");
    public static readonly Guid MFVideoFormat_MJPG = new("47504A4D-0000-0010-8000-00AA00389B71");
    public static readonly Guid MFVideoFormat_H264 = new("34363248-0000-0010-8000-00AA00389B71");
    public static readonly Guid MFVideoFormat_H265 = new("35363248-0000-0010-8000-00AA00389B71");
    public static readonly Guid MFVideoFormat_HEVC = new("43564548-0000-0010-8000-00AA00389B71");

    /// <summary>
    /// Returns the FFmpeg-side pixel-format string for the given MF
    /// subtype GUID, or <see langword="null"/> when the GUID isn't a
    /// recognised video format. Case-sensitive in spelling — these
    /// strings are passed verbatim to FFmpeg's <c>pixel_format</c> /
    /// <c>vcodec</c> options.
    /// </summary>
    public static string? Map(Guid subtypeGuid)
    {
        if (subtypeGuid == MFVideoFormat_NV12)
        {
            return "nv12";
        }

        if (subtypeGuid == MFVideoFormat_YUY2)
        {
            return "yuyv422";
        }

        if (subtypeGuid == MFVideoFormat_UYVY)
        {
            return "uyvy422";
        }

        if (subtypeGuid == MFVideoFormat_RGB24)
        {
            return "rgb24";
        }

        if (subtypeGuid == MFVideoFormat_RGB32)
        {
            return "bgra";
        }

        if (subtypeGuid == MFVideoFormat_ARGB32)
        {
            return "argb";
        }

        if (subtypeGuid == MFVideoFormat_I420 || subtypeGuid == MFVideoFormat_IYUV)
        {
            return "yuv420p";
        }

        if (subtypeGuid == MFVideoFormat_YV12)
        {
            return "yuv420p";
        }

        if (subtypeGuid == MFVideoFormat_MJPG)
        {
            return "mjpeg";
        }

        if (subtypeGuid == MFVideoFormat_H264)
        {
            return "h264";
        }

        if (subtypeGuid == MFVideoFormat_H265 || subtypeGuid == MFVideoFormat_HEVC)
        {
            return "hevc";
        }

        return null;
    }
}