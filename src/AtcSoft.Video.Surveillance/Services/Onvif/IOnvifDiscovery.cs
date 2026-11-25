namespace AtcSoft.Video.Surveillance.Services.Onvif;

/// <summary>
/// Finds the ONVIF cameras on this machine's networks.
/// </summary>
public interface IOnvifDiscovery
{
    /// <summary>
    /// Probes the networks and lists the devices that answered. Never throws for the network.
    /// </summary>
    /// <param name="listenTime">How long answers are waited for (3 s when left out).</param>
    /// <param name="cancellationToken">Cancels the search.</param>
    /// <returns>The devices, and whether any network card could send the probe.</returns>
    Task<OnvifDiscoveryResult> DiscoverAsync(
        TimeSpan? listenTime = null,
        CancellationToken cancellationToken = default);
}