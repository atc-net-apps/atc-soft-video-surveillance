namespace AtcSoft.VideoEngine;

/// <summary>
/// The steps of an FFmpeg install, in order.
/// </summary>
public enum FFmpegInstallStage
{
    /// <summary>The package is being downloaded.</summary>
    Downloading,

    /// <summary>The download is being checked against its SHA-256.</summary>
    Verifying,

    /// <summary>The libraries and tools are being unpacked.</summary>
    Extracting,

    /// <summary>FFmpeg is in place.</summary>
    Done,
}