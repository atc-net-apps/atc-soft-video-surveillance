namespace AtcSoft.VideoSurveillance.Services;

/// <summary>
/// Detects wired vs. Wi-Fi through the ONVIF Device service
/// (<c>GetNetworkInterfaces</c>). Time-boxed and non-throwing: any failure
/// (no ONVIF, wrong credentials, timeout, unknown interface type) yields
/// <see cref="CameraConnectionType.None"/>.
/// </summary>
/// <remarks>
/// Requests are SOAP 1.2 with a WS-Security UsernameToken (PasswordDigest).
/// The token timestamp is aligned to the camera clock first
/// (<c>GetSystemDateAndTime</c>, which ONVIF allows without authentication),
/// because many cameras reject tokens outside a small clock window.
/// </remarks>
public sealed partial class OnvifConnectionTypeDetector : ICameraConnectionTypeDetector
{
    private const string DeviceWsdlNamespace = "http://www.onvif.org/ver10/device/wsdl";
    private const string SoapNamespace = "http://www.w3.org/2003/05/soap-envelope";
    private const string WsseNamespace = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";
    private const string WsuNamespace = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd";
    private const string PasswordDigestType = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-username-token-profile-1.0#PasswordDigest";
    private const string Base64EncodingType = "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-soap-message-security-1.0#Base64Binary";

    // One client for the process when no HttpClient is injected (avoids socket
    // exhaustion). No proxy: cameras sit on private addresses that a system
    // proxy's local bypass rule usually doesn't cover.
    private static readonly HttpClient SharedHttpClient = new(new SocketsHttpHandler { UseProxy = false });

    private readonly HttpClient httpClient;
    private readonly OnvifConnectionTypeDetectorOptions options;
    private readonly ILogger<OnvifConnectionTypeDetector> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="OnvifConnectionTypeDetector"/> class
    /// using a process-wide <see cref="HttpClient"/> and default options.
    /// </summary>
    public OnvifConnectionTypeDetector(
        ILogger<OnvifConnectionTypeDetector>? logger = null)
        : this(SharedHttpClient, options: null, logger)
    {
    }

    public OnvifConnectionTypeDetector(
        HttpClient httpClient,
        OnvifConnectionTypeDetectorOptions? options = null,
        ILogger<OnvifConnectionTypeDetector>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        this.httpClient = httpClient;
        this.options = options ?? new OnvifConnectionTypeDetectorOptions();
        this.logger = logger ?? NullLogger<OnvifConnectionTypeDetector>.Instance;
    }

    /// <inheritdoc />
    [SuppressMessage("AsyncUsage", "AsyncFixer04:Fire-and-forget async call inside a using block", Justification = "Every probe is awaited in the finally block before the token source is disposed.")]
    public async Task<CameraConnectionType> DetectAsync(
        string ipAddress,
        string? userName,
        string? password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        {
            return CameraConnectionType.None;
        }

        using var total = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        total.CancelAfter(options.TotalTimeout);

        // Ask every candidate port at once whether it speaks ONVIF. A port
        // that is closed or filtered takes the full request timeout to say
        // nothing, so asking them one after the other let a few dead ports
        // use up the whole time budget before the right one was tried.
        var probes = new List<Task<OnvifPortAnswer?>>(options.Ports.Count);
        foreach (var port in options.Ports)
        {
            probes.Add(ProbePortAsync(ipAddress, port, total.Token));
        }

        try
        {
            // Take the first port in the configured order that answered, so
            // the credentials are only ever offered to a single port.
            foreach (var probe in probes)
            {
                var answer = await probe.ConfigureAwait(false);
                if (answer is null)
                {
                    continue;
                }

                // The port answered ONVIF; its verdict is final, even if "None".
                return await ReadConnectionTypeAsync(ipAddress, answer, userName, password, total.Token).ConfigureAwait(false);
            }

            return CameraConnectionType.None;
        }
        finally
        {
            // Stop the probes nobody is waiting for any more, and observe them.
            await total.CancelAsync().ConfigureAwait(false);
            await Task.WhenAll(probes).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Builds a SOAP 1.2 Device-service request, with a WS-Security
    /// UsernameToken when <paramref name="userName"/> is set.
    /// </summary>
    internal static string BuildEnvelope(
        string operation,
        string? userName,
        string? password,
        DateTimeOffset createdUtc,
        byte[] nonce)
    {
        XNamespace s = SoapNamespace;
        XNamespace tds = DeviceWsdlNamespace;

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
            new XAttribute(XNamespace.Xmlns + "tds", DeviceWsdlNamespace),
            header,
            new XElement(s + "Body", new XElement(tds + operation)));

        return envelope.ToString(SaveOptions.DisableFormatting);
    }

    /// <summary>
    /// WS-Security PasswordDigest: Base64(SHA-1(nonce + created + password)),
    /// where <paramref name="nonce"/> is the <em>raw</em> nonce bytes (not its
    /// Base64 text).
    /// </summary>
    [SuppressMessage("Security", "CA5350:Do Not Use Weak Cryptographic Algorithms", Justification = "SHA-1 is mandated by the WS-Security UsernameToken profile that ONVIF devices implement.")]
    [SuppressMessage("Security", "S4790:Using weak hashing algorithms is security-sensitive", Justification = "SHA-1 is mandated by the WS-Security UsernameToken profile that ONVIF devices implement.")]
    internal static string ComputePasswordDigest(
        byte[] nonce,
        string created,
        string password)
    {
        var createdBytes = Encoding.UTF8.GetBytes(created);
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        var buffer = new byte[nonce.Length + createdBytes.Length + passwordBytes.Length];
        nonce.CopyTo(buffer, 0);
        createdBytes.CopyTo(buffer, nonce.Length);
        passwordBytes.CopyTo(buffer, nonce.Length + createdBytes.Length);

        return Convert.ToBase64String(SHA1.HashData(buffer));
    }

    /// <summary>
    /// Asks one port for the camera's clock, which needs no credentials.
    /// Returns <see langword="null"/> when nothing ONVIF answers there.
    /// </summary>
    private async Task<OnvifPortAnswer?> ProbePortAsync(
        string ipAddress,
        int port,
        CancellationToken cancellationToken)
    {
        var uri = BuildDeviceServiceUri(ipAddress, port);
        if (uri is null)
        {
            return null;
        }

        var dateTimeResponse = await PostAsync(uri, "GetSystemDateAndTime", userName: null, password: null, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
        if (dateTimeResponse is null)
        {
            return null;
        }

        // Align the token timestamp to the camera clock; fall back to our own.
        var cameraUtc = OnvifNetworkInterfaceParser.ParseSystemDateAndTime(dateTimeResponse) ?? DateTimeOffset.UtcNow;
        return new OnvifPortAnswer(uri, port, cameraUtc);
    }

    private async Task<CameraConnectionType> ReadConnectionTypeAsync(
        string ipAddress,
        OnvifPortAnswer answer,
        string? userName,
        string? password,
        CancellationToken cancellationToken)
    {
        var interfacesResponse = await PostAsync(answer.Uri, "GetNetworkInterfaces", userName, password, answer.CameraUtc, cancellationToken).ConfigureAwait(false);
        if (interfacesResponse is null)
        {
            // ONVIF answered, but the interfaces call failed (e.g. credentials).
            return CameraConnectionType.None;
        }

        var connectionType = OnvifNetworkInterfaceParser.Parse(interfacesResponse, ipAddress);
        LogDetected(ipAddress, answer.Port, connectionType);
        return connectionType;
    }

    private async Task<string?> PostAsync(
        Uri uri,
        string operation,
        string? userName,
        string? password,
        DateTimeOffset createdUtc,
        CancellationToken cancellationToken)
    {
        var body = BuildEnvelope(operation, userName, password, createdUtc, RandomNumberGenerator.GetBytes(16));

        using var request = new HttpRequestMessage(HttpMethod.Post, uri);
        request.Content = new StringContent(body, Encoding.UTF8);
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(
            $"application/soap+xml; charset=utf-8; action=\"{DeviceWsdlNamespace}/{operation}\"");

        using var attempt = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        attempt.CancelAfter(options.RequestTimeout);

        try
        {
            using var response = await httpClient.SendAsync(request, attempt.Token).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                LogRequestRejected(operation, uri, (int)response.StatusCode);
                return null;
            }

            return await response.Content.ReadAsStringAsync(attempt.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Nothing listening, or another port already answered: expected.
            LogRequestTimedOut(operation, uri);
            return null;
        }
        catch (HttpRequestException ex)
        {
            LogRequestFailed(operation, uri, ex.Message);
            return null;
        }
    }

    [SuppressMessage("Security", "S5332:Using clear-text protocols is security-sensitive", Justification = "An ONVIF camera serves its device service over http on the camera's own port; the request carries no credentials")]
    private Uri? BuildDeviceServiceUri(
        string ipAddress,
        int port)
    {
        var host = ipAddress.Contains(':', StringComparison.Ordinal) && !ipAddress.StartsWith('[')
            ? $"[{ipAddress}]"
            : ipAddress;

        return Uri.TryCreate($"http://{host}:{port.ToString(CultureInfo.InvariantCulture)}{options.DeviceServicePath}", UriKind.Absolute, out var uri)
            ? uri
            : null;
    }

    /// <summary>
    /// A port that answered ONVIF, with the camera's clock.
    /// </summary>
    private sealed record OnvifPortAnswer(
        Uri Uri,
        int Port,
        DateTimeOffset CameraUtc);
}