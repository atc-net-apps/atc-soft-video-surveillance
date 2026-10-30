namespace AtcSoft.VideoSurveillance.Wpf.Helpers;

/// <summary>
/// What the camera and recording states from the server mean in this client.
/// </summary>
public static class HubStates
{
    /// <summary>
    /// Gets the tile state for a camera state; a state this client does not know is disconnected.
    /// </summary>
    public static Atc.Network.ConnectionState ToConnectionState(
        CameraConnectionState? state)
        => state?.Value switch
        {
            CameraConnectionState.Names.Connected => Atc.Network.ConnectionState.Connected,
            CameraConnectionState.Names.Connecting => Atc.Network.ConnectionState.Connecting,
            CameraConnectionState.Names.Reconnecting => Atc.Network.ConnectionState.Reconnecting,
            CameraConnectionState.Names.Error => Atc.Network.ConnectionState.ConnectionFailed,
            _ => Atc.Network.ConnectionState.Disconnected,
        };

    /// <summary>
    /// Gets a value indicating whether a camera is being recorded: until it is idle
    /// again, the tail after a motion included.
    /// </summary>
    public static bool IsRecording(RecordingStatusState state)
        => state.Value is RecordingStatusState.Names.Recording
            or RecordingStatusState.Names.RecordingMotion
            or RecordingStatusState.Names.RecordingPostMotion;

    /// <summary>
    /// Gets the state as the camera list shows it, in the chosen language. A
    /// state this client does not know yet is shown as the server writes it,
    /// with a capital.
    /// </summary>
    public static string ToText(CameraConnectionState? state)
        => state?.Value switch
        {
            null or "" => Translations.Disconnected,
            CameraConnectionState.Names.Connected => Translations.Connected,
            CameraConnectionState.Names.Connecting => Translations.Connecting,
            CameraConnectionState.Names.Disconnected => Translations.Disconnected,
            CameraConnectionState.Names.Reconnecting => Translations.Reconnecting,
            CameraConnectionState.Names.Error => Translations.Error,
            CameraConnectionState.Names.DeviceUnplugged => Translations.UsbDeviceUnplugged,
            var value => string.Concat(value[..1].ToUpperInvariant(), value[1..]),
        };

    /// <summary>
    /// Gets whether the client has the server's live updates
    /// (<see cref="SurveillanceEventService.IsConnected"/>), in the chosen language.
    /// </summary>
    public static string ServerText(bool connected)
        => string.Format(
            CultureInfo.CurrentCulture,
            Translations.LiveUpdates1,
            connected ? Translations.LiveUpdatesOn : Translations.LiveUpdatesConnecting);
}