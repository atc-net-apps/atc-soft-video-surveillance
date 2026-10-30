namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// The USB settings the camera form sends: only for a USB camera, and only
/// what it has. A field it leaves out keeps its value on the server.
/// </summary>
public static class UsbFields
{
    public static int? Size(
        bool isUsb,
        int value)
        => isUsb && value > 0 ? value : null;

    public static double? FrameRate(
        bool isUsb,
        double value)
        => isUsb && value > 0 ? value : null;

    public static string? Text(
        bool isUsb,
        string value)
        => isUsb ? value : null;

    public static bool? Flag(
        bool isUsb,
        bool value)
        => isUsb ? value : null;
}