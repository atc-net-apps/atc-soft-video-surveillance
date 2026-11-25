namespace AtcSoft.Video.Surveillance.Helpers;

/// <summary>
/// Builds the WS-Discovery probe for ONVIF cameras and reads their answers (ProbeMatches).
/// Elements are matched by local name, as devices differ in their prefixes.
/// </summary>
public static class WsDiscoveryParser
{
    /// <summary>
    /// The multicast address and port WS-Discovery probes are sent to.
    /// </summary>
    [SuppressMessage("Major Code Smell", "S1313:Do not hardcode IP address", Justification = "The WS-Discovery multicast group is fixed by the standard.")]
    public static readonly IPEndPoint MulticastEndPoint = new(IPAddress.Parse("239.255.255.250"), 3702);

    private const string SoapNamespace = "http://www.w3.org/2003/05/soap-envelope";
    private const string AddressingNamespace = "http://schemas.xmlsoap.org/ws/2004/08/addressing";
    private const string DiscoveryNamespace = "http://schemas.xmlsoap.org/ws/2005/04/discovery";
    private const string NetworkNamespace = "http://www.onvif.org/ver10/network/wsdl";

    /// <summary>
    /// Builds a probe for network video transmitters (cameras and encoders).
    /// </summary>
    /// <param name="messageId">The probe's id; answers name it.</param>
    /// <returns>The probe as text.</returns>
    public static string BuildProbe(Guid messageId)
    {
        XNamespace s = SoapNamespace;
        XNamespace a = AddressingNamespace;
        XNamespace d = DiscoveryNamespace;

        return new XElement(
            s + "Envelope",
            new XAttribute(XNamespace.Xmlns + "s", SoapNamespace),
            new XAttribute(XNamespace.Xmlns + "a", AddressingNamespace),
            new XAttribute(XNamespace.Xmlns + "d", DiscoveryNamespace),
            new XAttribute(XNamespace.Xmlns + "dn", NetworkNamespace),
            new XElement(
                s + "Header",
                new XElement(a + "Action", new XAttribute(s + "mustUnderstand", "1"), DiscoveryNamespace + "/Probe"),
                new XElement(a + "MessageID", "uuid:" + messageId.ToString("D")),
                new XElement(a + "ReplyTo", new XElement(a + "Address", AddressingNamespace + "/role/anonymous")),
                new XElement(a + "To", new XAttribute(s + "mustUnderstand", "1"), "urn:schemas-xmlsoap-org:ws:2005:04:discovery")),
            new XElement(
                s + "Body",
                new XElement(d + "Probe", new XElement(d + "Types", "dn:NetworkVideoTransmitter"))))
            .ToString(SaveOptions.DisableFormatting);
    }

    /// <summary>
    /// Reads the devices from an answer. A device is listed with its first device-service address
    /// on a private network (10/8, 172.16/12, 192.168/16, 169.254/16) or a host name: a camera never
    /// lives on a public address of the network it is found on.
    /// </summary>
    /// <param name="xml">The answer.</param>
    /// <returns>The devices in it.</returns>
    public static IReadOnlyList<OnvifDevice> ParseProbeMatches(string xml)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(xml);
        }
        catch (System.Xml.XmlException)
        {
            return [];
        }

        var devices = new List<OnvifDevice>();
        foreach (var match in document.Descendants().Where(e => e.Name.LocalName == "ProbeMatch"))
        {
            var endpoint = match.Descendants().FirstOrDefault(e => e.Name.LocalName == "Address")?.Value.Trim();
            var service = Words(Child(match, "XAddrs"))
                .Select(x => Uri.TryCreate(x, UriKind.Absolute, out var uri) ? uri : null)
                .FirstOrDefault(uri => uri is not null && IsOnLocalNetwork(uri));
            if (string.IsNullOrEmpty(endpoint) || service is null)
            {
                continue;
            }

            var scopes = Words(Child(match, "Scopes")).ToList();
            devices.Add(new OnvifDevice(endpoint, service, Scope(scopes, "name"), Scope(scopes, "hardware")));
        }

        return devices;
    }

    /// <summary>
    /// Tells whether an address is one a camera on this network can have: a private or link-local
    /// IPv4 address, or a name.
    /// </summary>
    /// <param name="uri">The device service's address.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    public static bool IsOnLocalNetwork(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);
        if (uri.HostNameType == UriHostNameType.Dns)
        {
            return true;
        }

        if (!IPAddress.TryParse(uri.Host, out var ip) || ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return false;
        }

        var b = ip.GetAddressBytes();
        return b[0] == 10 ||
               (b[0] == 172 && b[1] >= 16 && b[1] <= 31) ||
               (b[0] == 192 && b[1] == 168) ||
               (b[0] == 169 && b[1] == 254);
    }

    private static string? Child(
        XElement parent,
        string localName)
        => parent.Elements().FirstOrDefault(e => e.Name.LocalName == localName)?.Value;

    private static IEnumerable<string> Words(string? text)
        => (text ?? string.Empty).Split((char[])[' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);

    // onvif://www.onvif.org/name/Front%20Door → "Front Door"
    private static string Scope(
        List<string> scopes,
        string kind)
    {
        var prefix = "onvif://www.onvif.org/" + kind + "/";
        var scope = scopes.FirstOrDefault(s => s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        return scope is null ? string.Empty : Uri.UnescapeDataString(scope[prefix.Length..]).Replace('_', ' ');
    }
}