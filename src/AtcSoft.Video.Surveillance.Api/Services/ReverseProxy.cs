namespace AtcSoft.Video.Surveillance.Api.Services;

/// <summary>
/// The server behind a reverse proxy (IIS, Caddy, nginx, a NAS's own): the proxy says which client
/// it calls for (<c>X-Forwarded-For</c>) and whether that client came over HTTPS
/// (<c>X-Forwarded-Proto</c>). The server believes it only from a proxy it trusts: this machine
/// always, others named in <c>ReverseProxy:KnownProxies</c> (addresses) or
/// <c>ReverseProxy:KnownNetworks</c> (networks, such as Docker's <c>172.16.0.0/12</c>). The client's
/// own address then counts for the rate limits and the audit log, not the proxy's.
/// </summary>
public static class ReverseProxy
{
    /// <summary>
    /// The setting with the addresses of trusted proxies.
    /// </summary>
    public const string KnownProxiesKey = "ReverseProxy:KnownProxies";

    /// <summary>
    /// The setting with the networks of trusted proxies.
    /// </summary>
    public const string KnownNetworksKey = "ReverseProxy:KnownNetworks";

    /// <summary>
    /// Applies the trusted proxies to the forwarded headers' options. A value that is no address or
    /// network stops the server at startup with the value in the message.
    /// </summary>
    /// <param name="options">The forwarded headers' options.</param>
    /// <param name="configuration">The server's configuration.</param>
    public static void Configure(
        ForwardedHeadersOptions options,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(configuration);

        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

        // Every trusted proxy in a row is taken off, whatever their number; an untrusted one stops it.
        options.ForwardLimit = null;

        foreach (var value in Values(configuration, KnownProxiesKey))
        {
            if (!IPAddress.TryParse(value, out var address))
            {
                throw new InvalidOperationException($"{KnownProxiesKey}: '{value}' is no IP address.");
            }

            options.KnownProxies.Add(address);
        }

        foreach (var value in Values(configuration, KnownNetworksKey))
        {
            if (!System.Net.IPNetwork.TryParse(value, out var network))
            {
                throw new InvalidOperationException($"{KnownNetworksKey}: '{value}' is no network (such as 172.16.0.0/12).");
            }

            options.KnownIPNetworks.Add(network);
        }
    }

    // A list in appsettings.json or environment variables (ReverseProxy__KnownNetworks__0), or one
    // value with the entries separated by commas or semicolons (ReverseProxy__KnownNetworks).
    private static IEnumerable<string> Values(
        IConfiguration configuration,
        string key)
    {
        var section = configuration.GetSection(key);
        var values = section.GetChildren().Select(child => child.Value).Append(section.Value);

        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .SelectMany(value => value!.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }
}