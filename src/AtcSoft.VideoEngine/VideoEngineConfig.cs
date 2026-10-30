namespace AtcSoft.VideoEngine;

/// <summary>
/// Configuration for the video engine initialization.
/// </summary>
public sealed class VideoEngineConfig
{
    /// <summary>
    /// Gets or sets the path to FFmpeg native libraries.
    /// If <c>null</c>, the engine will attempt to auto-discover FFmpeg.
    /// </summary>
    public string? FFmpegPath { get; set; }

    /// <summary>
    /// Gets or sets the FFmpeg log level. Default is <see cref="Flyleaf.FFmpeg.LogLevel.Error"/>.
    /// </summary>
    public FFmpegLogLevel FFmpegLogLevel { get; set; } = FFmpegLogLevel.Error;

    /// <summary>
    /// Gets or sets the number of decoding threads (0 = auto).
    /// </summary>
    public int DecoderThreads { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether hardware-accelerated decoding is preferred.
    /// Individual streams can override this via <see cref="StreamOptions.HardwareAcceleration"/>.
    /// </summary>
    public bool PreferHardwareAcceleration { get; set; } = true;
}