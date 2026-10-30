namespace AtcSoft.VideoEngine;

/// <summary>
/// The major versions of the FFmpeg libraries the engine loads (from file names such as
/// <c>avutil-61.dll</c>). The bindings accept exactly one major version of each.
/// </summary>
public sealed record FFmpegLibraryVersions(
    int Avutil,
    int Avcodec,
    int Avformat,
    int Avdevice,
    int Avfilter,
    int Swscale,
    int Swresample)
{
    /// <summary>
    /// Gets the FFmpeg release line these libraries come from (libavutil 60 is FFmpeg 8, 61 is FFmpeg 9).
    /// </summary>
    public int FFmpegMajor
        => Avutil - 52;

    /// <summary>
    /// Gets each library's file-name stem and its major version.
    /// </summary>
    public IReadOnlyList<(string Library, int Major)> Libraries
        =>
        [
            ("avutil", Avutil),
            ("avcodec", Avcodec),
            ("avformat", Avformat),
            ("avdevice", Avdevice),
            ("avfilter", Avfilter),
            ("swscale", Swscale),
            ("swresample", Swresample),
        ];
}