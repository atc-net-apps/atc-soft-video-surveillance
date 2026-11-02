namespace AtcSoft.Video.Surveillance.Models.Settings;

/// <summary>
/// Where a network camera came from when it was added from a camera hub (motionEye on a
/// Raspberry Pi, ...): the hub and the camera's id on it. The camera plays its stream like any
/// network camera; the origin is what lets the app find it on the hub again and say where it is.
/// </summary>
public sealed class CameraHubOrigin
{
    /// <summary>
    /// Gets or sets the kind of hub (<see cref="Services.Hubs.CameraHubKinds"/>).
    /// </summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the hub's address, as its API is reached (<c>http://pi-garage:8765/</c>).
    /// </summary>
    public string Address { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the camera's id on the hub.
    /// </summary>
    public string CameraId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the camera's name on the hub, when it was added.
    /// </summary>
    public string? CameraName { get; set; }

    public CameraHubOrigin Clone()
        => new()
        {
            Kind = Kind,
            Address = Address,
            CameraId = CameraId,
            CameraName = CameraName,
        };

    public bool ValueEquals(CameraHubOrigin? other)
        => other is not null &&
           string.Equals(Kind, other.Kind, StringComparison.Ordinal) &&
           string.Equals(Address, other.Address, StringComparison.Ordinal) &&
           string.Equals(CameraId, other.CameraId, StringComparison.Ordinal) &&
           string.Equals(CameraName, other.CameraName, StringComparison.Ordinal);
}