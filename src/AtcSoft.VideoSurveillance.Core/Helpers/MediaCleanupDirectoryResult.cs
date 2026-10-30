namespace AtcSoft.VideoSurveillance.Helpers;

/// <summary>
/// Result of <see cref="MediaCleanupRunner.RemoveEmptyDirectoriesBelow"/>.
/// </summary>
/// <param name="RemovedDirectories">Full paths of empty directories removed.</param>
/// <param name="Errors">Per-directory errors encountered.</param>
public sealed record MediaCleanupDirectoryResult(
    IReadOnlyList<string> RemovedDirectories,
    IReadOnlyList<MediaCleanupRunError> Errors);