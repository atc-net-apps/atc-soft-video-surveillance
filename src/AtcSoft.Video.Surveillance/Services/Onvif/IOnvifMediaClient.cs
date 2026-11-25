namespace AtcSoft.Video.Surveillance.Services.Onvif;

/// <summary>
/// Reads a camera's ONVIF media profiles: which streams it offers, and their RTSP addresses.
/// </summary>
public interface IOnvifMediaClient
{
    /// <summary>
    /// Reads the profiles. Never throws for what the camera answers or does not answer; the
    /// outcome says how it went.
    /// </summary>
    /// <param name="address">The camera's address, as a person types it (192.168.1.64 or a host name).</param>
    /// <param name="port">The ONVIF port, or <see langword="null"/> to try the usual ones (80, 8080, 8000, 2020).</param>
    /// <param name="userName">The camera user; never logged.</param>
    /// <param name="password">Its password; never logged.</param>
    /// <param name="cancellationToken">Cancels the reading.</param>
    /// <returns>The result.</returns>
    Task<OnvifImportResult> ReadProfilesAsync(
        string address,
        int? port,
        string? userName,
        string? password,
        CancellationToken cancellationToken = default);
}