namespace AtcSoft.VideoSurveillance.Api.Services;

/// <summary>
/// Whether the server was given an HTTPS endpoint: an https address in
/// <c>urls</c> (<c>--urls</c>, <c>ASPNETCORE_URLS</c>), a Kestrel endpoint with
/// one, or an <c>https_port</c> (HTTPS ends at a proxy in front). Only then
/// does the server send plain HTTP on to HTTPS and tell browsers to stay there.
/// </summary>
public static class HttpsEndpoints
{
    public static bool IsConfigured(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (!string.IsNullOrWhiteSpace(configuration["https_port"]))
        {
            return true;
        }

        var urls = (configuration["urls"] ?? string.Empty)
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var endpointUrls = configuration
            .GetSection("Kestrel:Endpoints")
            .GetChildren()
            .Select(endpoint => endpoint["Url"] ?? string.Empty);

        return urls
            .Concat(endpointUrls)
            .Any(url => url.StartsWith("https://", StringComparison.OrdinalIgnoreCase));
    }
}