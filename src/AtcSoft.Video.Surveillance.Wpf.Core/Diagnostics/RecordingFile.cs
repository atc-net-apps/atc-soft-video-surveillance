namespace AtcSoft.Video.Surveillance.Wpf.Core.Diagnostics;

/// <summary>
/// A running recording as <see cref="RecordingGrowthWatch"/> sees it.
/// </summary>
/// <param name="CameraId">The camera.</param>
/// <param name="CameraName">The camera's name, for the log.</param>
/// <param name="FilePath">The file being written.</param>
/// <param name="Length">The file's length now, in bytes.</param>
public sealed record RecordingFile(
    Guid CameraId,
    string CameraName,
    string FilePath,
    long Length);