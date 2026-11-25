namespace AtcSoft.Video.Surveillance.Services.Onvif;

/// <summary>
/// Reads a camera's ONVIF media profiles over SOAP 1.2 (<see cref="OnvifSoap"/>): the device
/// service's port is found with <c>GetSystemDateAndTime</c> (which needs no sign-in and gives the
/// camera's clock, which the sign-in token follows), then <c>GetCapabilities</c> names the media
/// service, whose <c>GetProfiles</c> and <c>GetStreamUri</c> give the streams.
/// </summary>
/// <remarks>
/// The media service's address is taken with the host the person typed: a camera often names
/// another one of its addresses. The credentials are never logged, and the stream addresses keep
/// no user info.
/// </remarks>
public sealed partial class OnvifMediaClient : IOnvifMediaClient
{
    private static readonly HttpClient SharedHttpClient = new(new SocketsHttpHandler { UseProxy = false });
    private static readonly IReadOnlyList<int> UsualPorts = [80, 8080, 8000, 2020];
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan TotalTimeout = TimeSpan.FromSeconds(12);

    private readonly HttpClient httpClient;
    private readonly ILogger<OnvifMediaClient> logger;

    public OnvifMediaClient(ILogger<OnvifMediaClient>? logger = null)
        : this(SharedHttpClient, logger)
    {
    }

    public OnvifMediaClient(
        HttpClient httpClient,
        ILogger<OnvifMediaClient>? logger = null)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        this.logger = logger ?? NullLogger<OnvifMediaClient>.Instance;
    }

    /// <inheritdoc />
    [SuppressMessage("AsyncUsage", "AsyncFixer04:Fire-and-forget async call inside a using block", Justification = "Every probe is awaited in the finally block before the token source is disposed.")]
    public async Task<OnvifImportResult> ReadProfilesAsync(
        string address,
        int? port,
        string? userName,
        string? password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return OnvifImportResult.Failed(OnvifImportOutcome.NotOnvif);
        }

        using var total = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        total.CancelAfter(TotalTimeout);
        var host = address.Trim();

        var ports = port is { } given ? [given] : UsualPorts;
        var probes = new List<Task<Answer?>>(ports.Count);
        foreach (var candidate in ports)
        {
            probes.Add(ProbeAsync(host, candidate, total.Token));
        }

        try
        {
            var device = await FirstAnswerAsync(probes).ConfigureAwait(false);
            if (device is null)
            {
                return OnvifImportResult.Failed(total.IsCancellationRequested && !cancellationToken.IsCancellationRequested && port is not null
                    ? OnvifImportOutcome.Timeout
                    : OnvifImportOutcome.NotOnvif);
            }

            return await ReadAsync(host, device, userName, password, total.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return OnvifImportResult.Failed(OnvifImportOutcome.Timeout);
        }
        finally
        {
            await total.CancelAsync().ConfigureAwait(false);
            await Task.WhenAll(probes).ConfigureAwait(false);
        }
    }

    // The first port in the order tried that answered: the credentials go to that one only.
    private static async Task<Answer?> FirstAnswerAsync(
        List<Task<Answer?>> probes)
    {
        foreach (var probe in probes)
        {
            if (await probe.ConfigureAwait(false) is { } answer)
            {
                return answer;
            }
        }

        return null;
    }

    private async Task<OnvifImportResult> ReadAsync(
        string host,
        Answer device,
        string? userName,
        string? password,
        CancellationToken cancellationToken)
    {
        XNamespace tds = OnvifSoap.DeviceNamespace;
        XNamespace trt = OnvifSoap.MediaNamespace;
        XNamespace tt = OnvifSoap.SchemaNamespace;

        var capabilities = await PostAsync(
            device.Uri,
            new XElement(tds + "GetCapabilities", new XElement(tds + "Category", "Media")),
            userName,
            password,
            device.CameraUtc,
            cancellationToken).ConfigureAwait(false);
        if (capabilities.Refused)
        {
            return OnvifImportResult.Failed(OnvifImportOutcome.Unauthorized);
        }

        // The media service where the camera names it. A camera that names another address of its
        // own (behind port forwarding, or on another network card) is asked at the address and
        // port that answered.
        var media = OnvifProfileParser.ParseMediaAddress(capabilities.Body ?? string.Empty) is { } named
            ? string.Equals(named.Host, device.Uri.Host, StringComparison.OrdinalIgnoreCase)
                ? named
                : new UriBuilder(named) { Host = device.Uri.Host, Port = device.Uri.Port }.Uri
            : device.Uri;

        var profilesAnswer = await PostAsync(media, new XElement(trt + "GetProfiles"), userName, password, device.CameraUtc, cancellationToken).ConfigureAwait(false);
        if (profilesAnswer.Refused)
        {
            return OnvifImportResult.Failed(OnvifImportOutcome.Unauthorized);
        }

        var profiles = new List<OnvifStreamProfile>();
        foreach (var profile in OnvifProfileParser.ParseProfiles(profilesAnswer.Body ?? string.Empty))
        {
            var request = new XElement(
                trt + "GetStreamUri",
                new XElement(
                    trt + "StreamSetup",
                    new XElement(tt + "Stream", "RTP-Unicast"),
                    new XElement(tt + "Transport", new XElement(tt + "Protocol", "RTSP"))),
                new XElement(trt + "ProfileToken", profile.Token));
            var uriAnswer = await PostAsync(media, request, userName, password, device.CameraUtc, cancellationToken).ConfigureAwait(false);
            profiles.Add(profile with { StreamUri = WithoutUserInfo(OnvifProfileParser.ParseStreamUri(uriAnswer.Body ?? string.Empty)) });
        }

        var choice = OnvifStreamChoice.Choose(profiles);
        LogProfilesRead(host, profiles.Count, choice?.Sub is not null);
        return choice is null
            ? new OnvifImportResult(OnvifImportOutcome.NoRtspProfile, profiles, Choice: null)
            : new OnvifImportResult(OnvifImportOutcome.Read, profiles, choice);
    }

    // Asks one port for the camera's clock, which needs no credentials.
    [SuppressMessage("Security", "S5332:Using clear-text protocols is security-sensitive", Justification = "An ONVIF camera serves its device service over http on the camera's own port; this request carries no credentials")]
    private async Task<Answer?> ProbeAsync(
        string host,
        int port,
        CancellationToken cancellationToken)
    {
        var bracketed = host.Contains(':', StringComparison.Ordinal) && !host.StartsWith("[", StringComparison.Ordinal) ? $"[{host}]" : host;
        if (!Uri.TryCreate($"http://{bracketed}:{port.ToString(CultureInfo.InvariantCulture)}/onvif/device_service", UriKind.Absolute, out var uri))
        {
            return null;
        }

        XNamespace tds = OnvifSoap.DeviceNamespace;
        var answer = await PostAsync(uri, new XElement(tds + "GetSystemDateAndTime"), userName: null, password: null, DateTimeOffset.UtcNow, cancellationToken).ConfigureAwait(false);
        if (answer.Body is null)
        {
            return null;
        }

        return new Answer(uri, OnvifNetworkInterfaceParser.ParseSystemDateAndTime(answer.Body) ?? DateTimeOffset.UtcNow);
    }

    private async Task<Reply> PostAsync(
        Uri uri,
        XElement body,
        string? userName,
        string? password,
        DateTimeOffset createdUtc,
        CancellationToken cancellationToken)
    {
        var envelope = OnvifSoap.BuildEnvelope(body, userName, password, createdUtc, RandomNumberGenerator.GetBytes(16));
        using var request = new HttpRequestMessage(HttpMethod.Post, uri);
        request.Content = new StringContent(envelope, Encoding.UTF8);
        request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(
            $"application/soap+xml; charset=utf-8; action=\"{OnvifSoap.ActionOf(body)}\"");

        using var attempt = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        attempt.CancelAfter(RequestTimeout);
        try
        {
            using var response = await httpClient.SendAsync(request, attempt.Token).ConfigureAwait(false);
            var text = await response.Content.ReadAsStringAsync(attempt.Token).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return new Reply(text, Refused: false);
            }

            var refused = response.StatusCode == System.Net.HttpStatusCode.Unauthorized || OnvifProfileParser.IsNotAuthorized(text);
            LogRequestRefused(body.Name.LocalName, uri.Host, (int)response.StatusCode);
            return new Reply(Body: null, refused);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new Reply(Body: null, Refused: false);
        }
        catch (HttpRequestException ex)
        {
            LogRequestFailed(body.Name.LocalName, uri.Host, ex.Message);
            return new Reply(Body: null, Refused: false);
        }
    }

    private static Uri? WithoutUserInfo(Uri? uri)
        => uri is null || string.IsNullOrEmpty(uri.UserInfo)
            ? uri
            : new UriBuilder(uri) { UserName = string.Empty, Password = string.Empty }.Uri;

    [LoggerMessage(Level = LogLevel.Information, Message = "ONVIF profiles of {Host}: {Count} read, sub-stream found: {HasSubStream}")]
    private partial void LogProfilesRead(string host, int count, bool hasSubStream);

    [LoggerMessage(Level = LogLevel.Debug, Message = "ONVIF {Operation} on {Host} was answered with {StatusCode}")]
    private partial void LogRequestRefused(string operation, string host, int statusCode);

    [LoggerMessage(Level = LogLevel.Debug, Message = "ONVIF {Operation} on {Host} failed: {Reason}")]
    private partial void LogRequestFailed(string operation, string host, string reason);

    private sealed record Answer(
        Uri Uri,
        DateTimeOffset CameraUtc);

    private sealed record Reply(
        string? Body,
        bool Refused);
}