namespace AtcSoft.VideoEngine;

/// <summary>
/// What a look for FFmpeg found.
/// </summary>
/// <param name="Status">Whether the FFmpeg the engine needs was found.</param>
/// <param name="Folder">The folder with the FFmpeg the engine needs, or <see langword="null"/>.</param>
/// <param name="Expected">The library versions the engine needs.</param>
/// <param name="Found">Every folder with FFmpeg libraries, in the order looked in.</param>
public sealed record FFmpegCheck(
    FFmpegStatus Status,
    string? Folder,
    FFmpegLibraryVersions Expected,
    IReadOnlyList<FFmpegFolder> Found)
{
    /// <summary>
    /// Gets a description for a log or an error: what is needed and what was found where.
    /// </summary>
    /// <returns>The description, in English.</returns>
    public string Describe()
        => Status switch
        {
            FFmpegStatus.Ready => $"FFmpeg {Expected.FFmpegMajor} found in '{Folder}'",
            FFmpegStatus.WrongVersion =>
                $"FFmpeg {Expected.FFmpegMajor} (libavutil {Expected.Avutil}) is needed; found "
                + string.Join(", ", Found.Select(f => $"FFmpeg {f.Versions.FFmpegMajor} (libavutil {f.Versions.Avutil}) in '{f.Folder}'"))
                + $". Install FFmpeg {FFmpegRuntime.ReleaseVersion} into '{FFmpegRuntime.ManagedFolder}'",
            _ => $"FFmpeg {Expected.FFmpegMajor} is needed and was not found. Install FFmpeg {FFmpegRuntime.ReleaseVersion} into '{FFmpegRuntime.ManagedFolder}'",
        };
}