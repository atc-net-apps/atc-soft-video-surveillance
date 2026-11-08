namespace AtcSoft.Video.Surveillance.Models;

/// <summary>
/// Another CameraWall's folder, opened for the timeline (read only).
/// </summary>
/// <param name="Root">The folder that holds its <c>cameras.json</c>, or the folder chosen.</param>
/// <param name="Cameras">The cameras, and a lane for each camera folder no camera owns.</param>
/// <param name="RecordingsFolder">The folder of camera folders.</param>
/// <param name="AvailabilityFolder">Its availability history, or <see langword="null"/>.</param>
/// <param name="LogFolder">Its log folder, or <see langword="null"/>.</param>
public sealed record TimelineFolderSource(
    string Root,
    IReadOnlyList<TimelineCamera> Cameras,
    string RecordingsFolder,
    string? AvailabilityFolder,
    string? LogFolder);