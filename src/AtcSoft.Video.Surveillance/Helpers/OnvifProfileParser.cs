namespace AtcSoft.Video.Surveillance.Helpers;

/// <summary>
/// Reads the ONVIF answers the profile import needs. Elements are matched by local name, as
/// cameras differ in their namespace prefixes (and some in their namespaces).
/// </summary>
public static class OnvifProfileParser
{
    /// <summary>
    /// Reads the Media service's address from a <c>GetCapabilitiesResponse</c>.
    /// </summary>
    /// <param name="xml">The answer.</param>
    /// <returns>The address, or <see langword="null"/> when the camera named none.</returns>
    public static Uri? ParseMediaAddress(string xml)
    {
        var document = TryParse(xml);
        var address = document?
            .Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Media")?
            .Elements()
            .FirstOrDefault(e => e.Name.LocalName == "XAddr")?
            .Value
            .Trim();

        return Uri.TryCreate(address, UriKind.Absolute, out var uri) ? uri : null;
    }

    /// <summary>
    /// Reads the video profiles from a <c>GetProfilesResponse</c>, without their stream addresses.
    /// </summary>
    /// <param name="xml">The answer.</param>
    /// <returns>The profiles with a video encoder, in the camera's order.</returns>
    public static IReadOnlyList<OnvifStreamProfile> ParseProfiles(string xml)
    {
        var document = TryParse(xml);
        if (document is null)
        {
            return [];
        }

        var profiles = new List<OnvifStreamProfile>();
        foreach (var profile in document.Descendants().Where(e => e.Name.LocalName == "Profiles"))
        {
            var token = profile.Attribute("token")?.Value;
            var encoder = profile.Elements().FirstOrDefault(e => e.Name.LocalName == "VideoEncoderConfiguration");
            if (string.IsNullOrWhiteSpace(token) || encoder is null)
            {
                continue;
            }

            var resolution = encoder.Elements().FirstOrDefault(e => e.Name.LocalName == "Resolution");
            profiles.Add(new OnvifStreamProfile(
                token,
                Child(profile, "Name") ?? token,
                Child(encoder, "Encoding") ?? string.Empty,
                ToInt(Child(resolution, "Width")),
                ToInt(Child(resolution, "Height")),
                StreamUri: null));
        }

        return profiles;
    }

    /// <summary>
    /// Reads the stream's address from a <c>GetStreamUriResponse</c>.
    /// </summary>
    /// <param name="xml">The answer.</param>
    /// <returns>The address, or <see langword="null"/> when there is none.</returns>
    public static Uri? ParseStreamUri(string xml)
    {
        var address = TryParse(xml)?
            .Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "MediaUri")?
            .Elements()
            .FirstOrDefault(e => e.Name.LocalName == "Uri")?
            .Value
            .Trim();

        return Uri.TryCreate(address, UriKind.Absolute, out var uri) ? uri : null;
    }

    /// <summary>
    /// Tells whether an answer is a SOAP fault for credentials the camera refused.
    /// </summary>
    /// <param name="xml">The answer.</param>
    /// <returns><see langword="true"/> for a <c>NotAuthorized</c> fault.</returns>
    public static bool IsNotAuthorized(string xml)
        => TryParse(xml)?
            .Descendants()
            .Any(e => e.Name.LocalName == "Value" && e.Value.Contains("NotAuthorized", StringComparison.OrdinalIgnoreCase)) == true;

    private static string? Child(
        XElement? parent,
        string localName)
        => parent?.Elements().FirstOrDefault(e => e.Name.LocalName == localName)?.Value.Trim();

    private static int ToInt(string? value)
        => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : 0;

    private static XDocument? TryParse(string? xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return null;
        }

        try
        {
            return XDocument.Parse(xml);
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }
}