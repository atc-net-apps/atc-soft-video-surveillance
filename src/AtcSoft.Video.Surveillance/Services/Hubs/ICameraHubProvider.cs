namespace AtcSoft.Video.Surveillance.Services.Hubs;

/// <summary>
/// Lists the cameras of one kind of hub, and gets a picture of each.
/// </summary>
public interface ICameraHubProvider
{
    /// <summary>
    /// Gets the kind of hub (<see cref="CameraHubKinds"/>).
    /// </summary>
    string Kind { get; }

    /// <summary>
    /// Signs in to the hub and lists its cameras.
    /// </summary>
    /// <param name="address">What the user typed: a host, an address, or a URL.</param>
    /// <param name="userName">The hub's user.</param>
    /// <param name="password">Its password.</param>
    /// <param name="cancellationToken">Stops it.</param>
    Task<CameraHubListing> ListAsync(
        string address,
        string? userName,
        string? password,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the camera's current picture (JPEG), or <see langword="null"/>.
    /// </summary>
    Task<byte[]?> GetSnapshotAsync(
        Uri hub,
        string? userName,
        string? password,
        string cameraId,
        CancellationToken cancellationToken = default);
}