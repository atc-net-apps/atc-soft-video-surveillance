namespace AtcSoft.Video.Surveillance.Services.Onvif;

/// <summary>
/// The ONVIF devices found on the networks.
/// </summary>
/// <param name="Devices">The devices, one per device id.</param>
/// <param name="CouldProbe">
/// Whether any network card could send the probe; <see langword="false"/> in a container without
/// host networking, or a machine without a network.
/// </param>
public sealed record OnvifDiscoveryResult(
    IReadOnlyList<OnvifDevice> Devices,
    bool CouldProbe);