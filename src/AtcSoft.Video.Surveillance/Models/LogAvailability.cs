namespace AtcSoft.Video.Surveillance.Models;

/// <summary>
/// The availability history read from CameraWall's log: the app's starts and stops, and each
/// camera's changes by its display name.
/// </summary>
/// <param name="App">The app's starts and stops, oldest first.</param>
/// <param name="Cameras">Each camera's changes by its display name (any case), oldest first.</param>
public sealed record LogAvailability(
    IReadOnlyList<AvailabilityEntry> App,
    IReadOnlyDictionary<string, IReadOnlyList<AvailabilityEntry>> Cameras)
{
    /// <summary>
    /// Gets a camera's history from the log, with the app's starts and stops, up to a time: after
    /// it the app kept its own history.
    /// </summary>
    /// <param name="cameraName">The camera's display name.</param>
    /// <param name="before">Only changes before this time; <see langword="null"/> for all.</param>
    /// <returns>The changes, oldest first.</returns>
    public IReadOnlyList<AvailabilityEntry> For(
        string cameraName,
        DateTimeOffset? before)
        => [.. App
            .Concat(Cameras.GetValueOrDefault(cameraName) ?? [])
            .Where(e => before is null || e.Time < before)
            .OrderBy(e => e.Time),];
}