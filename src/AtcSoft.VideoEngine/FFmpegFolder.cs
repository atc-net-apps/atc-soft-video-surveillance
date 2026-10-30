namespace AtcSoft.VideoEngine;

/// <summary>
/// A folder with FFmpeg libraries and their versions.
/// </summary>
/// <param name="Folder">The folder.</param>
/// <param name="Versions">The library versions in it.</param>
public sealed record FFmpegFolder(
    string Folder,
    FFmpegLibraryVersions Versions);