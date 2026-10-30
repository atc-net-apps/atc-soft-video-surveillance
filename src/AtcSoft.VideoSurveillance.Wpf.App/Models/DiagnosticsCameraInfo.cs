namespace AtcSoft.VideoSurveillance.Wpf.App.Models;

public sealed class DiagnosticsCameraInfo
{
    public Guid Id { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public string? IpAddress { get; set; }

    public string? UsbDeviceId { get; set; }

    public string? UsbFriendlyName { get; set; }

    public string? ConnectionState { get; set; }

    public bool IsRecording { get; set; }
}