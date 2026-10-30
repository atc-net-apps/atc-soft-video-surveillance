namespace AtcSoft.VideoEngine;

/// <summary>
/// Provides read-only information about the current video stream.
/// </summary>
public sealed class VideoStreamInfo
{
    /// <summary>
    /// Gets the video width in pixels.
    /// </summary>
    public int Width { get; init; }

    /// <summary>
    /// Gets the video height in pixels.
    /// </summary>
    public int Height { get; init; }

    /// <summary>
    /// Gets the codec name (e.g., "h264", "hevc").
    /// </summary>
    public string? CodecName { get; init; }

    /// <summary>
    /// Gets the pixel format name (e.g., "yuv420p", "nv12").
    /// </summary>
    public string? PixelFormat { get; init; }

    /// <summary>
    /// Gets a value indicating whether hardware-accelerated decoding is active.
    /// </summary>
    public bool IsHardwareAccelerated { get; init; }

    /// <inheritdoc />
    public override string ToString()
        => $"{Width}x{Height} {CodecName} ({PixelFormat}){(IsHardwareAccelerated ? " [HW]" : string.Empty)}";
}