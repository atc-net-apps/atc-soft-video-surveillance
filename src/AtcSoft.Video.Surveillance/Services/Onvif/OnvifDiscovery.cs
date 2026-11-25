namespace AtcSoft.Video.Surveillance.Services.Onvif;

/// <summary>
/// Finds the ONVIF cameras on this machine's networks: a WS-Discovery probe is sent from each
/// IPv4 network card to the multicast group, and the answers that come back within the time
/// given are read. The probe goes no further than the local network (time to live 1).
/// </summary>
/// <remarks>
/// In a Docker container on a bridge network, the probe reaches no camera: discovery needs host
/// networking there. Reading a camera's profiles by its address works without it.
/// </remarks>
public sealed partial class OnvifDiscovery : IOnvifDiscovery
{
    private static readonly TimeSpan DefaultListenTime = TimeSpan.FromSeconds(3);

    private readonly ILogger<OnvifDiscovery> logger;

    public OnvifDiscovery(ILogger<OnvifDiscovery>? logger = null)
        => this.logger = logger ?? NullLogger<OnvifDiscovery>.Instance;

    /// <inheritdoc />
    public async Task<OnvifDiscoveryResult> DiscoverAsync(
        TimeSpan? listenTime = null,
        CancellationToken cancellationToken = default)
    {
        var addresses = LocalAddresses();
        var searches = new List<Task<IReadOnlyList<OnvifDevice>?>>(addresses.Count);
        foreach (var address in addresses)
        {
            searches.Add(ProbeAsync(WsDiscoveryParser.MulticastEndPoint, address, listenTime ?? DefaultListenTime, cancellationToken));
        }

        var answers = await Task.WhenAll(searches).ConfigureAwait(false);
        return Collect(answers, addresses.Count);
    }

    // One per device id, by address; whether any card could send the probe.
    private OnvifDiscoveryResult Collect(
        IReadOnlyList<OnvifDevice>?[] answers,
        int cards)
    {
        var sent = false;
        var byEndpoint = new Dictionary<string, OnvifDevice>(StringComparer.OrdinalIgnoreCase);
        foreach (var answer in answers)
        {
            if (answer is null)
            {
                continue;
            }

            sent = true;
            foreach (var device in answer)
            {
                byEndpoint.TryAdd(device.EndpointAddress, device);
            }
        }

        var devices = byEndpoint.Values.OrderBy(d => d.Address, StringComparer.Ordinal).ToList();

        LogDiscovered(devices.Count, cards);
        return new OnvifDiscoveryResult(devices, CouldProbe: sent);
    }

    /// <summary>
    /// Sends one probe from <paramref name="localAddress"/> to <paramref name="target"/> and reads
    /// the answers that arrive within <paramref name="listenTime"/>.
    /// </summary>
    /// <returns>The devices, or <see langword="null"/> when the probe could not be sent.</returns>
    internal async Task<IReadOnlyList<OnvifDevice>?> ProbeAsync(
        IPEndPoint target,
        IPAddress localAddress,
        TimeSpan listenTime,
        CancellationToken cancellationToken)
    {
        using var client = new UdpClient(new IPEndPoint(localAddress, 0));
        var bytes = Encoding.UTF8.GetBytes(WsDiscoveryParser.BuildProbe(Guid.NewGuid()));
        try
        {
            if (IsMulticast(target.Address))
            {
                client.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, localAddress.GetAddressBytes());
                client.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 1);
            }

            await client.SendAsync(bytes, target, cancellationToken).ConfigureAwait(false);
        }
        catch (SocketException ex)
        {
            LogProbeNotSent(localAddress.ToString(), ex.SocketErrorCode.ToString());
            return null;
        }

        var devices = new List<OnvifDevice>();
        using var listening = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        listening.CancelAfter(listenTime);
        while (true)
        {
            try
            {
                var answer = await client.ReceiveAsync(listening.Token).ConfigureAwait(false);
                devices.AddRange(WsDiscoveryParser.ParseProbeMatches(Encoding.UTF8.GetString(answer.Buffer)));
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return devices;
            }
            catch (SocketException)
            {
                // An ICMP "port unreachable" from a host with nothing on 3702: listen on.
            }
        }
    }

    // The IPv4 addresses of the network cards that are up and can send multicast.
    private static List<IPAddress> LocalAddresses()
        => NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up &&
                        n.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                        n.SupportsMulticast)
            .SelectMany(n => n.GetIPProperties().UnicastAddresses)
            .Select(a => a.Address)
            .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
            .Distinct()
            .ToList();

    private static bool IsMulticast(IPAddress address)
        => address.AddressFamily == AddressFamily.InterNetwork && address.GetAddressBytes()[0] is >= 224 and <= 239;

    [LoggerMessage(Level = LogLevel.Information, Message = "ONVIF discovery found {Count} device(s) from {Cards} network card(s)")]
    private partial void LogDiscovered(int count, int cards);

    [LoggerMessage(Level = LogLevel.Debug, Message = "ONVIF discovery could not send from {Address}: {Error}")]
    private partial void LogProbeNotSent(string address, string error);
}