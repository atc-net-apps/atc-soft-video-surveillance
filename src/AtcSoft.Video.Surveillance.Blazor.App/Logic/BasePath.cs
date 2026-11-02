namespace AtcSoft.Video.Surveillance.Blazor.App.Logic;

/// <summary>
/// The server can live under a path of a proxy's site (<c>https://nas/surveillance/</c>). The
/// generated API client asks for <c>/cameras</c>, which from such a base address means
/// <c>https://nas/cameras</c>: the path is put back in front.
/// </summary>
public static class BasePath
{
    /// <summary>
    /// Gets the address a request is sent to: below the base address's path when it has one and
    /// the request is not below it already.
    /// </summary>
    /// <param name="baseAddress">The server's address, such as <c>https://nas/surveillance/</c>.</param>
    /// <param name="requestUri">The address the client made.</param>
    /// <returns>The address to send to.</returns>
    public static Uri Keep(
        Uri baseAddress,
        Uri requestUri)
    {
        ArgumentNullException.ThrowIfNull(baseAddress);
        ArgumentNullException.ThrowIfNull(requestUri);

        var basePath = baseAddress.AbsolutePath.TrimEnd('/');
        if (basePath.Length == 0 ||
            !requestUri.IsAbsoluteUri ||
            !string.Equals(requestUri.Authority, baseAddress.Authority, StringComparison.OrdinalIgnoreCase) ||
            requestUri.AbsolutePath.Equals(basePath, StringComparison.Ordinal) ||
            requestUri.AbsolutePath.StartsWith(basePath + "/", StringComparison.Ordinal))
        {
            return requestUri;
        }

        var builder = new UriBuilder(requestUri) { Path = basePath + requestUri.AbsolutePath };
        return builder.Uri;
    }
}