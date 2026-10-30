namespace AtcSoft.VideoSurveillance.Helpers;

/// <summary>
/// Result of <see cref="RecordingLayoutReorganizer.Reorganize"/>.
/// </summary>
/// <param name="MovedRecordings">The number of recordings that were moved.</param>
/// <param name="MovedThumbnails">The number of thumbnails that were moved.</param>
/// <param name="Errors">The files that could not be moved, and why.</param>
public sealed record RecordingLayoutReorganizeResult(
    int MovedRecordings,
    int MovedThumbnails,
    IReadOnlyList<MediaCleanupRunError> Errors);