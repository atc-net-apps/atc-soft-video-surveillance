namespace AtcSoft.VideoSurveillance.Helpers;

/// <summary>
/// Result of <see cref="MediaCleanupRunner.CleanDirectory"/>.
/// </summary>
/// <param name="DeletedFiles">Full paths of media files removed.</param>
/// <param name="DeletedThumbnails">Full paths of companion thumbnails removed.</param>
/// <param name="BytesFreed">Total bytes freed (files + thumbnails).</param>
/// <param name="Errors">Per-file or per-enumeration errors encountered.</param>
public sealed record MediaCleanupRunResult(
    IReadOnlyList<string> DeletedFiles,
    IReadOnlyList<string> DeletedThumbnails,
    long BytesFreed,
    IReadOnlyList<MediaCleanupRunError> Errors)
{
    /// <summary>
    /// Gets a value indicating whether the reclaim pass exhausted all eligible
    /// files without reaching the target free-space goal. Only meaningful for
    /// results returned by <see cref="MediaCleanupRunner.ReclaimBySize(string, IReadOnlyCollection{string}, long, IReadOnlySet{string}, bool)"/>.
    /// </summary>
    public bool StillShort { get; init; }
}