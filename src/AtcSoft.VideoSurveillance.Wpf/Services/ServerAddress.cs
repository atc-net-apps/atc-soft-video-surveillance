namespace AtcSoft.VideoSurveillance.Wpf.Services;

/// <summary>
/// The address the client talks to. A server with HTTPS redirects plain HTTP to its https://
/// address, and an HTTP client drops the Authorization header when the scheme changes, so a
/// client set to the http:// address saw a 401 on every signed-in call. Asked once at start,
/// the server's redirect gives the address to use instead.
/// </summary>
public static class ServerAddress
{
    // Anonymous and cheap: the server answers it before anyone has signed in.
    private const string ProbePath = "auth/setup";

    /// <summary>
    /// Gets the address to use: the https:// address a plain http:// one is redirected to, or
    /// the address as it is (already https, no redirect, or no answer).
    /// </summary>
    public static async Task<Uri> ResolveAsync(
        HttpClient httpClient,
        Uri serverAddress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(serverAddress);

        if (!string.Equals(serverAddress.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            return serverAddress;
        }

        var probe = new Uri($"{serverAddress.AbsoluteUri.TrimEnd('/')}/{ProbePath}");
        try
        {
            using var response = await httpClient.GetAsync(probe, cancellationToken).ConfigureAwait(false);
            var answeredFrom = response.RequestMessage?.RequestUri;
            if (answeredFrom is null ||
                !string.Equals(answeredFrom.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
                !answeredFrom.AbsolutePath.EndsWith("/" + ProbePath, StringComparison.OrdinalIgnoreCase))
            {
                return serverAddress;
            }

            // The redirect keeps the path, so the base is what precedes the probe.
            var basePath = answeredFrom.AbsolutePath[..^(ProbePath.Length + 1)];
            return new Uri(answeredFrom.GetLeftPart(UriPartial.Authority) + basePath);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return serverAddress;
        }
    }
}