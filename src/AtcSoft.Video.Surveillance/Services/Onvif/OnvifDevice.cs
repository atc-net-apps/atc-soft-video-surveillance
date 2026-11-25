namespace AtcSoft.Video.Surveillance.Services.Onvif;

/// <summary>
/// An ONVIF device that answered a WS-Discovery probe on the network.
/// </summary>
/// <param name="EndpointAddress">The device's own id (<c>urn:uuid:...</c>), the same on every answer.</param>
/// <param name="ServiceUri">Its device service, where its profiles are read.</param>
/// <param name="Name">Its name, from its scopes, or empty.</param>
/// <param name="Hardware">Its model, from its scopes, or empty.</param>
public sealed record OnvifDevice(
    string EndpointAddress,
    Uri ServiceUri,
    string Name,
    string Hardware)
{
    /// <summary>
    /// Gets the device's address, as a camera's address is typed.
    /// </summary>
    public string Address => ServiceUri.Host;

    /// <summary>
    /// Gets the device service's port.
    /// </summary>
    public int Port => ServiceUri.Port;
}