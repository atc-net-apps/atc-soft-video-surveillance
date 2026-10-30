namespace AtcSoft.VideoEngine;

/// <summary>
/// A downloadable FFmpeg build: a zip with a top folder holding <c>bin/</c> (the shared
/// libraries, <c>ffmpeg.exe</c> and <c>ffprobe.exe</c>).
/// </summary>
/// <param name="DownloadUri">Where the zip is downloaded from.</param>
/// <param name="Sha256">The zip's SHA-256, lower-case hex; a download that differs is refused.</param>
/// <param name="Size">The zip's size in bytes, for progress.</param>
/// <param name="ReleaseVersion">The FFmpeg release in the zip.</param>
public sealed record FFmpegPackage(
    Uri DownloadUri,
    string Sha256,
    long Size,
    string ReleaseVersion);