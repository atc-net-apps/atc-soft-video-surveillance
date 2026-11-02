namespace AtcSoft.Video.Surveillance.Api.Domain.Security;

/// <summary>
/// The first admin, made on the server's own machine without the setup code: whoever sits at
/// the server (a browser on <c>http://localhost:8585</c> right after the install) could read
/// the code anyway. Everyone else needs the code from the data folder.
/// </summary>
/// <remarks>
/// A request counts as local only when it came over the loopback connection and no proxy
/// handled it: a proxy on the same machine connects from loopback too, so any
/// <c>X-Forwarded-*</c> header (or the <c>X-Original-*</c> ones the forwarded-headers
/// middleware leaves after reading them) means it came from somewhere else. A proxy on the
/// same machine that sends none of them cannot be told from a local browser:
/// <see cref="AllowLocalKey"/> turns local setup off for such a server.
/// </remarks>
public sealed class LocalSetup
{
    /// <summary>
    /// The configuration key that allows local setup (on by default). As an environment variable: <c>Setup__AllowLocal</c>.
    /// </summary>
    public const string AllowLocalKey = "Setup:AllowLocal";

    /// <summary>
    /// The name of the file in the server's data folder with the one-time setup code.
    /// </summary>
    public const string SetupCodeFileName = "setup-code.txt";

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalSetup"/> class.
    /// </summary>
    /// <param name="allowed">Whether a request from the server's own machine needs no setup code.</param>
    public LocalSetup(bool allowed)
        => IsAllowed = allowed;

    /// <summary>
    /// Gets a value indicating whether a request from the server's own machine needs no setup code.
    /// </summary>
    public bool IsAllowed { get; }

    /// <summary>
    /// Whether this request may create the first admin without the setup code.
    /// </summary>
    public bool Applies(HttpContext? context)
        => IsAllowed && context is not null && IsLocal(context);

    /// <summary>
    /// Whether a request came from the server's own machine, not through a proxy.
    /// </summary>
    public static bool IsLocal(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var address = context.Connection.RemoteIpAddress;
        if (address is null)
        {
            return false;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (!System.Net.IPAddress.IsLoopback(address))
        {
            return false;
        }

        foreach (var header in context.Request.Headers.Keys)
        {
            if (header.StartsWith("X-Forwarded-", StringComparison.OrdinalIgnoreCase) ||
                header.StartsWith("X-Original-", StringComparison.OrdinalIgnoreCase) ||
                header.Equals("Forwarded", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}