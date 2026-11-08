namespace AtcSoft.Video.Surveillance.CameraWall.Wpf.Timeline;

using TimelineCamera = AtcSoft.Video.Surveillance.Models.TimelineCamera;

/// <summary>
/// What a load of the timeline reads.
/// </summary>
/// <param name="Cameras">The cameras, each a lane.</param>
/// <param name="Folders">The recording folders.</param>
/// <param name="Sessions">The recordings in progress.</param>
/// <param name="AvailabilityFolder">The availability history, or <see langword="null"/>.</param>
/// <param name="LogFolder">The log folder, or <see langword="null"/>.</param>
internal sealed record TimelineSources(
    IReadOnlyList<TimelineCamera> Cameras,
    IReadOnlyList<string?> Folders,
    IReadOnlyList<RecordingSession> Sessions,
    string? AvailabilityFolder,
    string? LogFolder);