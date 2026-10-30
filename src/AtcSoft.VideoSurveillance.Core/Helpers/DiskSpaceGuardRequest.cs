namespace AtcSoft.VideoSurveillance.Helpers;

/// <summary>
/// What the disk-space guard keeps free, and where.
/// </summary>
/// <param name="RecordingFolders">Every recording folder: the global one (or its default) and each camera's own, nested ones included.</param>
/// <param name="SnapshotFolder">The snapshot folder, which also holds the time-lapse pictures, or <see langword="null"/>.</param>
/// <param name="MinFreeBytes">The space to keep free on each volume.</param>
/// <param name="ProtectedPaths">Full paths never to delete: recordings being written and their thumbnails (<see cref="DiskSpaceGuard.ProtectedPaths"/>).</param>
/// <param name="RecordingExtensions">The recording file extensions, with a leading dot.</param>
/// <param name="SnapshotExtensions">The snapshot file extensions, with a leading dot.</param>
public sealed record DiskSpaceGuardRequest(
    IReadOnlyList<string> RecordingFolders,
    string? SnapshotFolder,
    long MinFreeBytes,
    IReadOnlySet<string> ProtectedPaths,
    IReadOnlyCollection<string> RecordingExtensions,
    IReadOnlyCollection<string> SnapshotExtensions);