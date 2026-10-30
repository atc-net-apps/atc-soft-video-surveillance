namespace AtcSoft.VideoSurveillance.Wpf.Core.Helpers;

/// <summary>
/// Glyphs, names and link-health tint for the connection-type icon. The
/// glyphs are 14×14 stroke paths, drawn like the tile's quick-action icons.
/// </summary>
public static class ConnectionTypeIconHelper
{
    // RJ45 socket: body outline plus four contact pins.
    private const string LanGeometry =
        "M1,3 L13,3 L13,10 L10,10 L10,12.5 L4,12.5 L4,10 L1,10 Z " +
        "M4,5.5 L4,7.5 M6,5.5 L6,7.5 M8,5.5 L8,7.5 M10,5.5 L10,7.5";

    // Three signal arcs above a dot.
    private const string WifiGeometry =
        "M0.5,5.5 A9,9 0 0 1 13.5,5.5 " +
        "M2.8,8 A6,6 0 0 1 11.2,8 " +
        "M5,10.5 A3,3 0 0 1 9,10.5 " +
        "M7,12.6 L7,12.8";

    // USB trident: stem with arrowhead, round-tipped and square-tipped branches.
    private const string UsbGeometry =
        "M7,1 L7,13 M5.3,2.8 L7,1 L8.7,2.8 " +
        "M7,9.5 L3,7 L3,5.2 M2,4.2 A1,1 0 1 0 4,4.2 A1,1 0 1 0 2,4.2 " +
        "M7,7.5 L11,5.5 L11,4 M10,2.4 L12,2.4 L12,4 L10,4 Z";

    private static readonly Brush NormalBrush = CreateFrozenBrush(Colors.White);
    private static readonly Brush WarningBrush = CreateFrozenBrush(Color.FromRgb(0xFF, 0xC1, 0x07));
    private static readonly Brush ErrorBrush = CreateFrozenBrush(Color.FromRgb(0xF4, 0x43, 0x36));

    /// <summary>
    /// Gets the path data for a connection type, or <see langword="null"/>
    /// for <see cref="CameraConnectionType.None"/> (no icon).
    /// </summary>
    public static string? GetGeometryData(CameraConnectionType connectionType)
        => connectionType switch
        {
            CameraConnectionType.Lan => LanGeometry,
            CameraConnectionType.Wifi => WifiGeometry,
            CameraConnectionType.Usb => UsbGeometry,
            _ => null,
        };

    /// <summary>
    /// Gets the link-health tint for a connection state.
    /// </summary>
    public static ConnectionTypeIconTint GetTint(
        ConnectionState connectionState)
        => connectionState switch
        {
            ConnectionState.Connecting or
            ConnectionState.Reconnecting or
            ConnectionState.Disconnecting => ConnectionTypeIconTint.Warning,
            ConnectionState.Disconnected or
            ConnectionState.ConnectionFailed or
            ConnectionState.ReconnectionFailed => ConnectionTypeIconTint.Error,
            _ => ConnectionTypeIconTint.Normal,
        };

    /// <summary>
    /// Gets the brush for a link-health tint.
    /// </summary>
    public static Brush GetBrush(ConnectionTypeIconTint tint)
        => tint switch
        {
            ConnectionTypeIconTint.Warning => WarningBrush,
            ConnectionTypeIconTint.Error => ErrorBrush,
            _ => NormalBrush,
        };

    /// <summary>
    /// Gets the localized name of a connection type.
    /// </summary>
    public static string GetDisplayName(CameraConnectionType connectionType)
        => connectionType switch
        {
            CameraConnectionType.Lan => Translations.ConnectionTypeLan,
            CameraConnectionType.Wifi => Translations.ConnectionTypeWifi,
            CameraConnectionType.Usb => Translations.ConnectionTypeUsb,
            _ => Translations.ConnectionTypeNone,
        };

    private static SolidColorBrush CreateFrozenBrush(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}