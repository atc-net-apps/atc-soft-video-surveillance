namespace AtcSoft.VideoSurveillance.Api.Domain.Recordings;

/// <summary>
/// A recording the server has, finished or in progress, and the camera it belongs to.
/// </summary>
/// <param name="CameraId">The camera, or <see cref="Guid.Empty"/> for a folder no camera has.</param>
/// <param name="CameraName">The camera's name, or empty.</param>
/// <param name="FullPath">The video file.</param>
/// <param name="RelativePath">The path clients name it by: below the recordings root, with forward slashes.</param>
/// <param name="StartedAt">When it started, in UTC (the server names its files in UTC).</param>
/// <param name="LastWrittenAt">When it was last written, in UTC: its end, for a finished one.</param>
/// <param name="Length">Its size in bytes; 0 for one in progress.</param>
/// <param name="InProgress">Whether it is still being made.</param>
public sealed record RecordingFile(
    Guid CameraId,
    string CameraName,
    string FullPath,
    string RelativePath,
    DateTimeOffset StartedAt,
    DateTimeOffset LastWrittenAt,
    long Length,
    bool InProgress);