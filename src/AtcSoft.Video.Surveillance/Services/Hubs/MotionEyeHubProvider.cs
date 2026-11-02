namespace AtcSoft.Video.Surveillance.Services.Hubs;

/// <summary>
/// Lists the cameras of motionEye and motionEyeOS: <c>GET /config/list</c>, and each camera's
/// picture from <c>GET /picture/{id}/current</c>. A camera is played from its MJPEG stream, which
/// <c>motion</c> serves on the camera's own streaming port.
/// </summary>
/// <remarks>
/// motionEye 0.44 and later signs in with <c>POST /login</c> and a session cookie. Older ones
/// (0.42, the last of motionEyeOS) have no sign-in for an API client: each request carries
/// <c>_username</c> and <c>_signature</c>, a SHA-1 of the request keyed with the password
/// (<see cref="Sign"/>). That way had security holes, closed by 0.44.0; it is used only when the hub
/// knows no other, and the listing says so (<see cref="CameraHubListing.OlderSignIn"/>).
/// </remarks>
public sealed class MotionEyeHubProvider : ICameraHubProvider
{
    /// <summary>
    /// motionEye's port when the address names none.
    /// </summary>
    public const int DefaultPort = 8765;

    // motionEye's administrator when no user is given.
    private const string DefaultUser = "admin";

    // One client for the process when none is injected; no proxy (hubs sit on private addresses)
    // and no cookie jar (each call signs in and sends its own session).
    private static readonly HttpClient SharedHttpClient = new(new SocketsHttpHandler { UseProxy = false, UseCookies = false })
    {
        Timeout = TimeSpan.FromSeconds(10),
    };

    // What the older signature keeps of a path, a body and a key; the rest becomes '-'.
    private static readonly Regex SignatureCharacters = new(@"[^a-zA-Z0-9/?_.=&{}\[\]"":, -]", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    private readonly HttpClient httpClient;

    public MotionEyeHubProvider()
        : this(SharedHttpClient)
    {
    }

    public MotionEyeHubProvider(HttpClient httpClient)
        => this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

    /// <inheritdoc />
    public string Kind => CameraHubKinds.MotionEye;

    /// <summary>
    /// Gets the hub's address from what a user typed: <c>pi-garage</c>, <c>192.168.1.50</c>,
    /// <c>pi-garage:9000</c> or a full URL. Without a scheme it is HTTP on <see cref="DefaultPort"/>.
    /// </summary>
    /// <param name="address">What the user typed.</param>
    /// <returns>The address, ending in a slash, or <see langword="null"/> when it is no address.</returns>
    public static Uri? ToHubAddress(string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return null;
        }

        var text = address.Trim();
        var hasScheme = text.Contains("://", StringComparison.Ordinal);
        if (!Uri.TryCreate(hasScheme ? text : Uri.UriSchemeHttp + "://" + text, UriKind.Absolute, out var uri) ||
            !(string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal) || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)) ||
            string.IsNullOrEmpty(uri.Host))
        {
            return null;
        }

        var builder = new UriBuilder(uri);
        if (!hasScheme && uri.IsDefaultPort)
        {
            builder.Port = DefaultPort;
        }

        if (!builder.Path.EndsWith('/'))
        {
            builder.Path += "/";
        }

        builder.Query = string.Empty;
        builder.Fragment = string.Empty;
        return builder.Uri;
    }

    /// <summary>
    /// The older motionEye's signature of a request (motionEye 0.42's <c>compute_signature</c>): SHA-1
    /// of <c>method:path:body:key</c>, the path with its query sorted and <c>_signature</c> left out,
    /// and every character outside a small set turned into <c>-</c>.
    /// </summary>
    /// <param name="method">The HTTP method.</param>
    /// <param name="pathAndQuery">The request's path and query.</param>
    /// <param name="body">The request's body, or <see langword="null"/>.</param>
    /// <param name="key">The password.</param>
    /// <returns>The signature, lower-case hex.</returns>
    public static string Sign(
        string method,
        string pathAndQuery,
        string? body,
        string key)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(pathAndQuery);
        ArgumentNullException.ThrowIfNull(key);

        var at = pathAndQuery.IndexOf('?', StringComparison.Ordinal);
        var path = at < 0 ? pathAndQuery : pathAndQuery[..at];
        if (at >= 0)
        {
            var query = pathAndQuery[(at + 1)..]
                .Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Select(pair => pair.Split('=', 2))
                .Select(pair => (Name: Uri.UnescapeDataString(pair[0]), Value: pair.Length > 1 ? Uri.UnescapeDataString(pair[1].Replace('+', ' ')) : string.Empty))
                .Where(q => !string.Equals(q.Name, "_signature", StringComparison.Ordinal))
                .OrderBy(q => q.Name, StringComparer.Ordinal)
                .Select(q => q.Name + "=" + Quote(q.Value));
            path += "?" + string.Join('&', query);
        }

        var signedBody = body is null || body.StartsWith("---", StringComparison.Ordinal) ? string.Empty : SignatureCharacters.Replace(body, "-");
        var text = $"{method}:{SignatureCharacters.Replace(path, "-")}:{signedBody}:{SignatureCharacters.Replace(key, "-")}";
#pragma warning disable CA5350, S4790 // motionEye's older sign-in is SHA-1 by its own definition; it is used only for such a hub.
        return Convert.ToHexStringLower(SHA1.HashData(Encoding.UTF8.GetBytes(text)));
#pragma warning restore CA5350, S4790
    }

    /// <inheritdoc />
    public async Task<CameraHubListing> ListAsync(
        string address,
        string? userName,
        string? password,
        CancellationToken cancellationToken = default)
    {
        if (ToHubAddress(address) is not { } hub)
        {
            return CameraHubListing.Failed(CameraHubOutcome.Unreachable, Kind, new UriBuilder(Uri.UriSchemeHttp, "invalid").Uri);
        }

        try
        {
            var session = await SignInAsync(hub, userName, password, cancellationToken).ConfigureAwait(false);
            if (session.Outcome != CameraHubOutcome.Listed)
            {
                return CameraHubListing.Failed(session.Outcome, Kind, hub);
            }

            using var request = session.Request(hub, "config/list");
            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            {
                return CameraHubListing.Failed(CameraHubOutcome.SignInRefused, Kind, hub);
            }

            if (!response.IsSuccessStatusCode)
            {
                return CameraHubListing.Failed(CameraHubOutcome.NotThisKind, Kind, hub);
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (json.Contains("\"unauthorized\"", StringComparison.Ordinal))
            {
                return CameraHubListing.Failed(CameraHubOutcome.SignInRefused, Kind, hub);
            }

            return ReadCameras(hub, json) is { } cameras
                ? new CameraHubListing(CameraHubOutcome.Listed, Kind, hub, cameras) { OlderSignIn = session.Older }
                : CameraHubListing.Failed(CameraHubOutcome.NotThisKind, Kind, hub);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return CameraHubListing.Failed(CameraHubOutcome.Unreachable, Kind, hub);
        }
    }

    /// <inheritdoc />
    public async Task<byte[]?> GetSnapshotAsync(
        Uri hub,
        string? userName,
        string? password,
        string cameraId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(hub);
        ArgumentException.ThrowIfNullOrEmpty(cameraId);

        try
        {
            var session = await SignInAsync(hub, userName, password, cancellationToken).ConfigureAwait(false);
            if (session.Outcome != CameraHubOutcome.Listed)
            {
                return null;
            }

            using var request = session.Request(hub, $"picture/{Uri.EscapeDataString(cameraId)}/current");
            using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode && response.Content.Headers.ContentType?.MediaType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true
                ? await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false)
                : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    /// <summary>
    /// Reads motionEye's camera list (<c>{"cameras": [...]}</c>).
    /// </summary>
    /// <param name="hub">The hub, for the streams' host.</param>
    /// <param name="json">The answer of <c>GET /config/list</c>.</param>
    /// <returns>The cameras, or <see langword="null"/> when it is not motionEye's list.</returns>
    public static IReadOnlyList<HubCamera>? ReadCameras(
        Uri hub,
        string json)
    {
        ArgumentNullException.ThrowIfNull(hub);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }

        using (document)
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("cameras", out var list) ||
                list.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            return [.. list.EnumerateArray().Where(c => c.ValueKind == JsonValueKind.Object).Select(c => ReadCamera(hub, c))];
        }
    }

    private static HubCamera ReadCamera(
        Uri hub,
        JsonElement camera)
    {
        var id = Text(camera, "id") ?? string.Empty;
        var name = Text(camera, "name") ?? id;
        var proto = Text(camera, "proto");
        var enabled = Flag(camera, "enabled") ?? true;
        var framesPerSecond = Number(camera, "streaming_framerate") ?? Number(camera, "framerate");
        var resolution = Text(camera, "resolution");

        // A simple MJPEG camera is its own stream; a camera of another motionEye is played from there.
        if (string.Equals(proto, "mjpeg", StringComparison.Ordinal) &&
            Uri.TryCreate(Text(camera, "url"), UriKind.Absolute, out var url))
        {
            return new HubCamera(id, name, WithoutCredentials(url), HubStreamKind.Mjpeg, null, null, resolution, framesPerSecond, IsUsb: false, enabled ? HubCameraProblem.None : HubCameraProblem.Disabled);
        }

        if (proto is not ("v4l2" or "mmal" or "netcam"))
        {
            return new HubCamera(id, name, null, HubStreamKind.Mjpeg, null, null, resolution, framesPerSecond, IsUsb: false, HubCameraProblem.OnAnotherHub);
        }

        var problem = !enabled
            ? HubCameraProblem.Disabled
            : Flag(camera, "video_streaming") == false
                ? HubCameraProblem.StreamingOff
                : HubCameraProblem.None;
        var port = Number(camera, "streaming_port");
        var stream = port is > 0 and <= 65535
            ? new UriBuilder(Uri.UriSchemeHttp, hub.Host, port.Value, "/").Uri
            : null;
        var authenticated = Text(camera, "streaming_auth_mode") is { } mode && !string.Equals(mode, "disabled", StringComparison.Ordinal);
        return new HubCamera(
            id,
            name,
            stream,
            HubStreamKind.Mjpeg,
            authenticated ? Text(camera, "streaming_username") : null,
            authenticated ? Text(camera, "streaming_password") : null,
            resolution,
            framesPerSecond,
            IsUsb: string.Equals(proto, "v4l2", StringComparison.Ordinal),
            stream is null && problem == HubCameraProblem.None ? HubCameraProblem.StreamingOff : problem);
    }

    // POST /login: a session cookie (0.44 and later); an older motionEye answers without one, and
    // its requests are signed instead.
    private async Task<HubSession> SignInAsync(
        Uri hub,
        string? userName,
        string? password,
        CancellationToken cancellationToken)
    {
        var user = string.IsNullOrWhiteSpace(userName) ? DefaultUser : userName.Trim();
        using var content = new FormUrlEncodedContent(
        [
            new KeyValuePair<string, string>("username", user),
            new KeyValuePair<string, string>("password", password ?? string.Empty),
        ]);
        using var response = await httpClient.PostAsync(new Uri(hub, "login"), content, cancellationToken).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            return HubSession.Refused;
        }

        var cookie = response.Headers.TryGetValues("Set-Cookie", out var cookies)
            ? cookies.Select(c => c.Split(';')[0].Trim()).FirstOrDefault(c => c.StartsWith("user=", StringComparison.Ordinal))
            : null;
        if (response.IsSuccessStatusCode && cookie is not null)
        {
            return new HubSession(CameraHubOutcome.Listed, cookie, user, password, Older: false);
        }

        return response.IsSuccessStatusCode && await IsMotionEyeAsync(hub, cancellationToken).ConfigureAwait(false)
            ? new HubSession(CameraHubOutcome.Listed, Cookie: null, user, password, Older: true)
            : HubSession.NotMotionEye;
    }

    private async Task<bool> IsMotionEyeAsync(
        Uri hub,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(hub, cancellationToken).ConfigureAwait(false);
        var page = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return page.Contains("motionEye", StringComparison.OrdinalIgnoreCase);
    }

    // Python's quote(value, safe="!'()*~"), as motionEye's older signature encodes a query value.
    private static string Quote(string value)
    {
        var text = new StringBuilder();
        foreach (var b in Encoding.UTF8.GetBytes(value))
        {
            var c = (char)b;
            if (char.IsAsciiLetterOrDigit(c) || "_.-~!'()*".Contains(c, StringComparison.Ordinal))
            {
                text.Append(c);
            }
            else
            {
                text.Append('%').Append(b.ToString("X2", CultureInfo.InvariantCulture));
            }
        }

        return text.ToString();
    }

    private static Uri WithoutCredentials(Uri uri)
        => string.IsNullOrEmpty(uri.UserInfo) ? uri : new UriBuilder(uri) { UserName = string.Empty, Password = string.Empty }.Uri;

    private static string? Text(
        JsonElement element,
        string name)
        => element.TryGetProperty(name, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.GetRawText(),
                _ => null,
            }
            : null;

    private static int? Number(
        JsonElement element,
        string name)
        => element.TryGetProperty(name, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.Number when value.TryGetInt32(out var number) => number,
                JsonValueKind.String when int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) => number,
                _ => null,
            }
            : null;

    private static bool? Flag(
        JsonElement element,
        string name)
        => element.TryGetProperty(name, out var value)
            ? value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null,
            }
            : null;

    // How requests to one hub are signed in: with the session cookie, or signed the older way.
    private sealed record HubSession(
        CameraHubOutcome Outcome,
        string? Cookie,
        string? User,
        string? Password,
        bool Older)
    {
        public static HubSession Refused { get; } = new(CameraHubOutcome.SignInRefused, null, null, null, Older: false);

        public static HubSession NotMotionEye { get; } = new(CameraHubOutcome.NotThisKind, null, null, null, Older: false);

        public HttpRequestMessage Request(
            Uri hub,
            string relative)
        {
            var uri = new Uri(hub, relative);
            if (Older)
            {
                var unsigned = new UriBuilder(uri) { Query = "_username=" + Uri.EscapeDataString(User ?? DefaultUser) }.Uri;
                var signature = Sign("GET", unsigned.PathAndQuery, body: null, Password ?? string.Empty);
                uri = new UriBuilder(unsigned) { Query = unsigned.Query.TrimStart('?') + "&_signature=" + signature }.Uri;
            }

            var request = new HttpRequestMessage(HttpMethod.Get, uri);
            if (Cookie is not null)
            {
                request.Headers.Add("Cookie", Cookie);
            }

            return request;
        }
    }
}