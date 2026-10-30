namespace AtcSoft.VideoEngine;

/// <summary>
/// Whether the FFmpeg the engine needs was found.
/// </summary>
public enum FFmpegStatus
{
    /// <summary>A folder has exactly the libraries the engine needs.</summary>
    Ready,

    /// <summary>FFmpeg was found, but not the version the engine needs (or not all of it).</summary>
    WrongVersion,

    /// <summary>No FFmpeg was found.</summary>
    Missing,
}