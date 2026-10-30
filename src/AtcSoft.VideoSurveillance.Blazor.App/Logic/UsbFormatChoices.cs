namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// What the camera form offers for a USB camera, from what the camera tells
/// about itself, and how a camera's source is shown in a list.
/// </summary>
public static class UsbFormatChoices
{
    private const int PreferredWidth = 1280;
    private const int PreferredHeight = 720;
    private const int LargestDefaultHeight = 1080;

    /// <summary>
    /// Gets the picture sizes a camera offers, each once, largest first.
    /// </summary>
    public static IReadOnlyList<(int Width, int Height)> Sizes(
        IReadOnlyCollection<UsbStreamFormat>? capabilities)
        => [.. (capabilities ?? [])
            .Where(c => c is { Width: > 0, Height: > 0 })
            .Select(c => (c.Width, c.Height))
            .Distinct()
            .OrderByDescending(s => (long)s.Width * s.Height)];

    /// <summary>
    /// Gets the frame rates a camera offers at one size, highest first.
    /// </summary>
    public static IReadOnlyList<double> FrameRates(
        IReadOnlyCollection<UsbStreamFormat>? capabilities,
        int width,
        int height)
        => [.. (capabilities ?? [])
            .Where(c => c.Width == width && c.Height == height && c.FrameRate > 0)
            .Select(c => c.FrameRate)
            .Distinct()
            .OrderByDescending(rate => rate)];

    /// <summary>
    /// Gets the format a new camera starts with: 1280 x 720 when the camera
    /// has it (enough to see who is there, light to encode and watch),
    /// otherwise the largest size up to full HD, at its highest frame rate.
    /// </summary>
    /// <returns>The format, or all zeros to leave it to the camera.</returns>
    public static (int Width, int Height, double FrameRate) Default(
        IReadOnlyCollection<UsbStreamFormat>? capabilities)
    {
        var sizes = Sizes(capabilities);
        if (sizes.Count == 0)
        {
            return (0, 0, 0);
        }

        var size = sizes.Contains((PreferredWidth, PreferredHeight))
            ? (Width: PreferredWidth, Height: PreferredHeight)
            : sizes.FirstOrDefault(s => s.Height <= LargestDefaultHeight, sizes[^1]);

        var rates = FrameRates(capabilities, size.Width, size.Height);
        return (size.Width, size.Height, rates.Count > 0 ? rates[0] : 0);
    }

    /// <summary>
    /// Gets how a camera's source is shown in a list: a USB camera by its
    /// device name, a network camera by its address without credentials.
    /// </summary>
    public static string DescribeSource(
        bool isUsb,
        string? usbName,
        string? protocol,
        string? host,
        int port,
        string? path)
    {
        if (isUsb)
        {
            return string.IsNullOrWhiteSpace(usbName) ? "USB" : string.Format(CultureInfo.CurrentCulture, Translations.UsbSource1, usbName);
        }

        var scheme = string.IsNullOrWhiteSpace(protocol) ? "rtsp" : protocol.ToLowerInvariant();
        var trimmedPath = string.IsNullOrWhiteSpace(path) ? string.Empty : "/" + path.TrimStart('/');

        return string.Create(CultureInfo.InvariantCulture, $"{scheme}://{host}:{port}{trimmedPath}");
    }
}