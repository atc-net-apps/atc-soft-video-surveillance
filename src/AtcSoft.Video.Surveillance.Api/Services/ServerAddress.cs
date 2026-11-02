namespace AtcSoft.Video.Surveillance.Api.Services;

/// <summary>
/// Where the server listens when nothing says otherwise: <see cref="DefaultUrl"/>. Port 8585, not
/// ASP.NET's 5000: Synology uses 5000 for its own web interface, and macOS for AirPlay.
/// </summary>
public static class ServerAddress
{
    /// <summary>
    /// The server's address when none is configured: this machine only, port 8585. Other machines
    /// reach it through <c>--urls http://0.0.0.0:8585</c>, <c>ASPNETCORE_URLS</c> (the Docker image
    /// sets it) or a Kestrel endpoint.
    /// </summary>
    public const string DefaultUrl = "http://localhost:8585";

    /// <summary>
    /// Gets a value indicating whether the server was given an address of its own: <c>urls</c>
    /// (<c>--urls</c>, <c>ASPNETCORE_URLS</c>), <c>http_ports</c> or <c>https_ports</c>
    /// (<c>ASPNETCORE_HTTP_PORTS</c>, set by Microsoft's container images), or a Kestrel endpoint.
    /// </summary>
    public static bool IsConfigured(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return !string.IsNullOrWhiteSpace(configuration["urls"]) ||
               !string.IsNullOrWhiteSpace(configuration["http_ports"]) ||
               !string.IsNullOrWhiteSpace(configuration["https_ports"]) ||
               configuration.GetSection("Kestrel:Endpoints").GetChildren().Any();
    }
}