namespace AtcSoft.VideoSurveillance.Helpers;

/// <summary>
/// Helper class for building camera stream URIs and source locators.
/// </summary>
public static class CameraUriHelper
{
    /// <summary>
    /// Builds a URI for the specified camera configuration.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the camera's <see cref="ConnectionSettings.Source"/>
    /// is not <see cref="CameraSource.Network"/> (USB cameras must be
    /// resolved through <see cref="BuildSourceLocator"/> instead).
    /// </exception>
    public static Uri BuildUri(CameraConfiguration camera)
    {
        ArgumentNullException.ThrowIfNull(camera);
        return camera.BuildUri();
    }

    /// <summary>
    /// Builds a URI from individual components.
    /// </summary>
    public static Uri BuildUri(
        CameraProtocol protocol,
        string ipAddress,
        int port,
        string? path = null,
        string? userName = null,
        string? password = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(ipAddress);

        var scheme = protocol.ToScheme();
        var userInfo = BuildUserInfo(userName, password);
        var pathSegment = BuildPath(path);

        return new Uri($"{scheme}://{userInfo}{BracketIfIPv6(ipAddress)}:{port}{pathSegment}");
    }

    /// <summary>
    /// Gets the default port for the specified protocol.
    /// USB cameras have no port — see
    /// <see cref="GetDefaultPort(CameraSource)"/>.
    /// </summary>
    public static int GetDefaultPort(CameraProtocol protocol) => protocol switch
    {
        CameraProtocol.Rtsp => 554,
        CameraProtocol.Http => 80,
        CameraProtocol.Https => 443,
        _ => 554,
    };

    /// <summary>
    /// Source-aware default-port helper. Returns 0 for non-network
    /// sources so callers don't accidentally write 554 into a USB
    /// camera's <see cref="ConnectionSettings.Port"/>.
    /// </summary>
    public static int GetDefaultPort(CameraSource source) => source switch
    {
        CameraSource.Network => 554,
        _ => 0,
    };

    /// <summary>
    /// Builds the FFmpeg-ready locator for the given camera. Handles
    /// both network and USB sources transparently.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// USB camera has no <see cref="UsbConnectionSettings.DeviceId"/>.
    /// </exception>
    public static SourceLocator BuildSourceLocator(CameraConfiguration camera)
    {
        ArgumentNullException.ThrowIfNull(camera);

        return camera.Connection.Source switch
        {
            CameraSource.Usb => BuildUsbLocator(camera),
            _ => new SourceLocator(camera.BuildUri()),
        };
    }

    private static SourceLocator BuildUsbLocator(CameraConfiguration camera)
    {
        var usb = camera.Connection.Usb
                  ?? throw new InvalidOperationException(
                      "USB camera has no UsbConnectionSettings.");

        if (string.IsNullOrWhiteSpace(usb.FriendlyName) && string.IsNullOrWhiteSpace(usb.DeviceId))
        {
            throw new InvalidOperationException(
                "USB camera requires either a FriendlyName or a DeviceId.");
        }

        // FFmpeg dshow takes either the friendly name or the symbolic
        // link. Prefer the friendly name so logs read naturally; the
        // demuxer falls back to the device id only when names collide.
        var deviceName = !string.IsNullOrWhiteSpace(usb.FriendlyName)
            ? usb.FriendlyName
            : usb.DeviceId;

        // FFmpeg dshow accepts a combined `video=X:audio=Y` spec to open
        // the video device and the companion audio capture in one call.
        // We require BOTH PreferAudio and AudioDeviceName: an opt-in flag
        // without a name is meaningless, and a name without the flag is
        // treated as deliberately disabled (the dialog can keep the name
        // while the user toggles audio off without losing it).
        var includeAudio = usb.PreferAudio && !string.IsNullOrWhiteSpace(usb.AudioDeviceName);
        var rawDeviceSpec = includeAudio
            ? $"video={deviceName}:audio={usb.AudioDeviceName}"
            : $"video={deviceName}";

        // The synthetic Uri keeps log lines and exception messages
        // tidy. Escaping is loose: the value is for human reading.
        var placeholder = new Uri($"dshow:{Uri.EscapeDataString(deviceName)}", UriKind.Absolute);

        var format = usb.Format;
        var videoSize = FormatVideoSize(format);
        var frameRate = FormatFrameRate(format);

        // We deliberately do NOT forward Format.PixelFormat to FFmpeg
        // for USB sources. The Atc.Wpf.Hardware picker enumerates via
        // WinRT MediaCapture (Media Foundation), which exposes MF's
        // *transcoded* format list — e.g. an MJPG-only USB 2.0 webcam
        // appears to MF as offering NV12 / YUY2 / RGB24 because MF
        // performs the conversion in-process. dshow only sees the
        // camera's raw output, so asking it for pixel_format=nv12 on
        // such a camera makes the filter graph fail with
        // "Could not set video options". Letting dshow pick its own
        // format at the requested resolution + framerate works for
        // every USB camera we've tested. The saved Format.PixelFormat
        // is still kept on the model for forensic / UI purposes.
        return new SourceLocator(
            uri: placeholder,
            inputFormat: "dshow",
            rawDeviceSpec: rawDeviceSpec,
            videoSize: videoSize,
            frameRate: frameRate,
            pixelFormat: null,
            includesAudio: includeAudio);
    }

    private static string? FormatVideoSize(UsbStreamFormat? format)
    {
        if (format is not { Width: > 0, Height: > 0 })
        {
            return null;
        }

        return string.Create(
            CultureInfo.InvariantCulture,
            $"{format.Width}x{format.Height}");
    }

    private static string? FormatFrameRate(UsbStreamFormat? format)
    {
        if (format is not { FrameRate: > 0 })
        {
            return null;
        }

        return format.FrameRate.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string BuildUserInfo(
        string? userName,
        string? password)
    {
        if (string.IsNullOrEmpty(userName))
        {
            return string.Empty;
        }

        var escapedUser = Uri.EscapeDataString(userName);
        var escapedPassword = Uri.EscapeDataString(password ?? string.Empty);

        return $"{escapedUser}:{escapedPassword}@";
    }

    private static string BuildPath(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return string.Empty;
        }

        return $"/{path.TrimStart('/')}";
    }

    private static string BracketIfIPv6(string host)
        => host.StartsWith('[') ? host : host.Contains(':', StringComparison.Ordinal) ? $"[{host}]" : host;
}