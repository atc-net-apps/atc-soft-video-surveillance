namespace AtcSoft.VideoSurveillance.Models;

/// <summary>
/// A capture-format triple negotiated with a USB camera: resolution,
/// frame rate and pixel format. Matches an FFmpeg dshow/v4l2 option set
/// (<c>video_size</c>, <c>framerate</c>, <c>pixel_format</c>).
/// </summary>
public class UsbStreamFormat
{
    [Range(1, 8192, ErrorMessage = "Width must be between 1 and 8192.")]
    public int Width { get; set; }

    [Range(1, 8192, ErrorMessage = "Height must be between 1 and 8192.")]
    public int Height { get; set; }

    /// <summary>
    /// Frame rate in Hz. Use double so non-integer rates such as 29.97
    /// and 59.94 round-trip cleanly.
    /// </summary>
    [Range(0.1, 1000.0, ErrorMessage = "FrameRate must be positive.")]
    public double FrameRate { get; set; }

    /// <summary>
    /// FFmpeg pixel-format string (e.g. <c>yuyv422</c>, <c>nv12</c>,
    /// <c>mjpeg</c>, <c>h264</c>). Pass-through to FFmpeg without any
    /// enum mapping so we don't have to track every new format.
    /// </summary>
    public string PixelFormat { get; set; } = string.Empty;

    /// <inheritdoc />
    public override string ToString()
        => $"{Width.ToString(CultureInfo.InvariantCulture)}x{Height.ToString(CultureInfo.InvariantCulture)}@{FrameRate.ToString("0.##", CultureInfo.InvariantCulture)} {PixelFormat}".TrimEnd();

    public UsbStreamFormat Clone()
        => new()
        {
            Width = Width,
            Height = Height,
            FrameRate = FrameRate,
            PixelFormat = PixelFormat,
        };

    public void CopyFrom(UsbStreamFormat source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Width = source.Width;
        Height = source.Height;
        FrameRate = source.FrameRate;
        PixelFormat = source.PixelFormat;
    }

    [SuppressMessage("Major Bug", "S1244:Floating point numbers should not be tested for equality", Justification = "Value equality: the same format is the same stored frame rate, exactly")]
    public bool ValueEquals(UsbStreamFormat? other)
    {
        if (other is null)
        {
            return false;
        }

        return Width == other.Width &&
               Height == other.Height &&
               FrameRate.Equals(other.FrameRate) &&
               string.Equals(PixelFormat, other.PixelFormat, StringComparison.Ordinal);
    }
}