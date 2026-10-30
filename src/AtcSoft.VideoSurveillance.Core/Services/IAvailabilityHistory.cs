namespace AtcSoft.VideoSurveillance.Services;

/// <summary>
/// The history of whether each camera could be recorded, as a timeline needs it to explain a
/// gap: the camera's own changes and the server's starts and stops.
/// </summary>
public interface IAvailabilityHistory
{
    /// <summary>
    /// Gets a camera's history, with the server's starts and stops.
    /// </summary>
    /// <param name="cameraId">The camera.</param>
    /// <returns>The changes, oldest first.</returns>
    IReadOnlyList<AvailabilityEntry> For(Guid cameraId);
}