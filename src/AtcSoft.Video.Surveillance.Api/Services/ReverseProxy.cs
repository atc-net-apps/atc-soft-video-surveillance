namespace AtcSoft.Video.Surveillance.Api.Services;

/// <summary>
/// The server behind a reverse proxy (IIS, Caddy, nginx, a NAS's own): the proxy says which client
/// it calls for (<c>X-Forwarded-For</c>) and whether that client came over HTTPS
/// (<c>X-Forwarded-Proto</c>). The server believes it only from a proxy it trusts: this machine
/// always, others named in <c>ReverseProxy:KnownProxies</c> (addresses) or
/// <c>ReverseProxy:KnownNetworks</c> (networks, such as Docker's <c>172.16.0.0/12</c>). The client's
/// own address then counts for the rate limits and the audit log, not the proxy's.
/// <para>
/// The server can also live under a path of the proxy's site (<c>https://nas/surveillance/</c>): a
/// proxy that takes the path off says which in <c>X-Forwarded-Prefix</c>, and one that passes it on
/// is matched by the <see cref="PathBaseKey"/> setting.
/// </para>
/// </summary>
public static class ReverseProxy
{
    /// <summary>
    /// The setting with the path the server lives under when a proxy passes it on, such as
    /// <c>/surveillance</c>. Empty (the default) is the root.
    /// </summary>
    public const string PathBaseKey = "PathBase";

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

        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedPrefix;

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

    /// <summary>
    /// Gets the path the server lives under from <see cref="PathBaseKey"/>: with one leading
    /// slash and no trailing one, or <see langword="null"/> for the root. A value with a query, a
    /// fragment or a scheme stops the server at startup.
    /// </summary>
    /// <param name="configuration">The server's configuration.</param>
    public static PathString? PathBase(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var value = configuration[PathBaseKey]?.Trim().Trim('/');
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        if (value.IndexOfAny(['?', '#', ':']) >= 0)
        {
            throw new InvalidOperationException($"{PathBaseKey}: '{value}' is no path (such as /surveillance).");
        }

        return new PathString("/" + value);
    }

    /// <summary>
    /// Reads the forwarded headers of trusted proxies, then takes off the configured path the
    /// server lives under. Call first, so everything after it sees the client and the path as the
    /// browser asked for them.
    /// </summary>
    /// <param name="app">The server.</param>
    public static void UseReverseProxy(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseForwardedHeaders();
        if (PathBase(app.Configuration) is { } pathBase)
        {
            app.UsePathBase(pathBase);
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