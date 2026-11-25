namespace AtcSoft.Video.Surveillance.Services.Onvif;

/// <summary>
/// The SOAP 1.2 requests ONVIF devices answer: an envelope with a WS-Security UsernameToken
/// (PasswordDigest), its time aligned to the camera's clock by the caller.
/// </summary>
public static class OnvifSoap
{
    public const string SoapNamespace = "http://www.w3.org/2003/05/soap-envelope";
    public const string DeviceNamespace = "http://www.onvif.org/ver10/device/wsdl";
    public const string MediaNamespace = "http://www.onvif.org/ver10/media/wsdl";
    public const string SchemaNamespace = "http://www.onvif.org/ver10/schema";

    private const string WsseNamespace = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";
    private const string WsuNamespace = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd";
    private const string PasswordDigestType = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordDigest";
    private const string Base64EncodingType = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-soap-message-security-1.0#Base64Binary";

    /// <summary>
    /// Builds a request: <paramref name="body"/> in the SOAP body, with a UsernameToken when
    /// <paramref name="userName"/> is set.
    /// </summary>
    /// <param name="body">The operation's element, in its service's namespace.</param>
    /// <param name="userName">The camera user, or nothing for a request that needs none.</param>
    /// <param name="password">Its password.</param>
    /// <param name="createdUtc">The token's time, as the camera's clock has it.</param>
    /// <param name="nonce">The token's random bytes.</param>
    /// <returns>The envelope as text.</returns>
    public static string BuildEnvelope(
        XElement body,
        string? userName,
        string? password,
        DateTimeOffset createdUtc,
        byte[] nonce)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(nonce);

        XNamespace s = SoapNamespace;
        var header = new XElement(s + "Header");
        if (!string.IsNullOrEmpty(userName))
        {
            XNamespace wsse = WsseNamespace;
            XNamespace wsu = WsuNamespace;
            var created = createdUtc.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

            header.Add(
                new XElement(
                    wsse + "Security",
                    new XAttribute(s + "mustUnderstand", "1"),
                    new XElement(
                        wsse + "UsernameToken",
                        new XElement(wsse + "Username", userName),
                        new XElement(
                            wsse + "Password",
                            new XAttribute("Type", PasswordDigestType),
                            ComputePasswordDigest(nonce, created, password ?? string.Empty)),
                        new XElement(
                            wsse + "Nonce",
                            new XAttribute("EncodingType", Base64EncodingType),
                            Convert.ToBase64String(nonce)),
                        new XElement(wsu + "Created", created))));
        }

        var envelope = new XElement(
            s + "Envelope",
            new XAttribute(XNamespace.Xmlns + "s", SoapNamespace),
            new XAttribute(XNamespace.Xmlns + "tds", DeviceNamespace),
            new XAttribute(XNamespace.Xmlns + "trt", MediaNamespace),
            new XAttribute(XNamespace.Xmlns + "tt", SchemaNamespace),
            header,
            new XElement(s + "Body", body));

        return envelope.ToString(SaveOptions.DisableFormatting);
    }

    /// <summary>
    /// WS-Security PasswordDigest: Base64(SHA-1(nonce + created + password)), where
    /// <paramref name="nonce"/> is the <em>raw</em> nonce bytes (not its Base64 text).
    /// </summary>
    /// <param name="nonce">The raw nonce.</param>
    /// <param name="created">The token's time, as it is written in the token.</param>
    /// <param name="password">The password.</param>
    /// <returns>The digest, Base64.</returns>
    [SuppressMessage("Security", "CA5350:Do Not Use Weak Cryptographic Algorithms", Justification = "SHA-1 is mandated by the WS-Security UsernameToken profile that ONVIF devices implement.")]
    [SuppressMessage("Security", "S4790:Using weak hashing algorithms is security-sensitive", Justification = "SHA-1 is mandated by the WS-Security UsernameToken profile that ONVIF devices implement.")]
    public static string ComputePasswordDigest(
        byte[] nonce,
        string created,
        string password)
    {
        ArgumentNullException.ThrowIfNull(nonce);
        ArgumentNullException.ThrowIfNull(created);
        ArgumentNullException.ThrowIfNull(password);

        var createdBytes = Encoding.UTF8.GetBytes(created);
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        var buffer = new byte[nonce.Length + createdBytes.Length + passwordBytes.Length];
        nonce.CopyTo(buffer, 0);
        createdBytes.CopyTo(buffer, nonce.Length);
        passwordBytes.CopyTo(buffer, nonce.Length + createdBytes.Length);

        return Convert.ToBase64String(SHA1.HashData(buffer));
    }

    /// <summary>
    /// Gets the SOAP action of an operation, for the request's content type.
    /// </summary>
    /// <param name="body">The operation's element.</param>
    /// <returns>The action, such as <c>http://www.onvif.org/ver10/media/wsdl/GetProfiles</c>.</returns>
    public static string ActionOf(XElement body)
    {
        ArgumentNullException.ThrowIfNull(body);
        return body.Name.NamespaceName + "/" + body.Name.LocalName;
    }
}