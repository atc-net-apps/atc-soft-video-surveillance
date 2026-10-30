namespace AtcSoft.VideoSurveillance.Helpers;

/// <summary>
/// Parses ONVIF Device-service responses for connection-type detection.
/// Elements are matched by local name so vendor namespace prefixes don't matter.
/// </summary>
/// <remarks>
/// Classification follows the ONVIF schema (tt:NetworkInterface): an
/// <c>Extension/Dot11</c> configuration or <c>Extension/InterfaceType</c> 71
/// (IANA ieee80211) means Wi-Fi; <c>InterfaceType</c> 6 (IANA ethernetCsmacd)
/// means wired. Anything else yields <see cref="CameraConnectionType.None"/>:
/// the detector never guesses from interface names.
/// </remarks>
public static class OnvifNetworkInterfaceParser
{
    private const int IanaEthernetCsmacd = 6;
    private const int IanaIeee80211 = 71;

    /// <summary>
    /// Parses a <c>GetNetworkInterfacesResponse</c>. The interface whose IPv4
    /// address equals <paramref name="ipAddress"/> is used; failing that, the
    /// only enabled interface, or all enabled interfaces when they agree.
    /// </summary>
    public static CameraConnectionType Parse(
        string soapResponse,
        string? ipAddress)
    {
        var document = TryLoad(soapResponse);
        if (document is null)
        {
            return CameraConnectionType.None;
        }

        var interfaces = document
            .Descendants()
            .Where(e => e.Name.LocalName == "NetworkInterfaces")
            .ToList();

        if (interfaces.Count == 0)
        {
            return CameraConnectionType.None;
        }

        if (!string.IsNullOrWhiteSpace(ipAddress))
        {
            var matching = interfaces.FirstOrDefault(i => GetIPv4Addresses(i).Contains(ipAddress, StringComparer.OrdinalIgnoreCase));
            if (matching is not null)
            {
                return Classify(matching);
            }
        }

        var types = interfaces
            .Where(IsEnabled)
            .Select(Classify)
            .Distinct()
            .ToList();

        return types.Count == 1
            ? types[0]
            : CameraConnectionType.None;
    }

    /// <summary>
    /// Parses a <c>GetSystemDateAndTimeResponse</c> into the camera's UTC
    /// clock, used to align the WS-Security timestamp. Returns
    /// <see langword="null"/> when the response has no UTC date/time.
    /// </summary>
    public static DateTimeOffset? ParseSystemDateAndTime(string soapResponse)
    {
        var utc = TryLoad(soapResponse)?
            .Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "UTCDateTime");

        if (utc is null)
        {
            return null;
        }

        int? Read(string name)
            => int.TryParse(
                utc.Descendants().FirstOrDefault(e => e.Name.LocalName == name)?.Value,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var value)
                ? value
                : null;

        if (Read("Year") is not { } year ||
            Read("Month") is not { } month ||
            Read("Day") is not { } day ||
            Read("Hour") is not { } hour ||
            Read("Minute") is not { } minute ||
            Read("Second") is not { } second)
        {
            return null;
        }

        try
        {
            return new DateTimeOffset(year, month, day, hour, minute, second, TimeSpan.Zero);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static XDocument? TryLoad(string soapResponse)
    {
        if (string.IsNullOrWhiteSpace(soapResponse))
        {
            return null;
        }

        try
        {
            return XDocument.Parse(soapResponse);
        }
        catch (XmlException)
        {
            return null;
        }
    }

    private static bool IsEnabled(XElement networkInterface)
    {
        var enabled = networkInterface
            .Elements()
            .FirstOrDefault(e => e.Name.LocalName == "Enabled");

        return enabled is null ||
               !string.Equals(enabled.Value.Trim(), "false", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> GetIPv4Addresses(
        XElement networkInterface)
        => networkInterface
            .Elements()
            .Where(e => e.Name.LocalName == "IPv4")
            .Descendants()
            .Where(e => e.Name.LocalName == "Address")
            .Select(e => e.Value.Trim());

    private static CameraConnectionType Classify(XElement networkInterface)
    {
        var extension = networkInterface
            .Elements()
            .FirstOrDefault(e => e.Name.LocalName == "Extension");

        if (extension is null)
        {
            return CameraConnectionType.None;
        }

        if (extension.Elements().Any(e => e.Name.LocalName == "Dot11"))
        {
            return CameraConnectionType.Wifi;
        }

        var interfaceType = extension
            .Elements()
            .FirstOrDefault(e => e.Name.LocalName == "InterfaceType");

        if (!int.TryParse(interfaceType?.Value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var ianaType))
        {
            return CameraConnectionType.None;
        }

        return ianaType switch
        {
            IanaIeee80211 => CameraConnectionType.Wifi,
            IanaEthernetCsmacd => CameraConnectionType.Lan,
            _ => CameraConnectionType.None,
        };
    }
}