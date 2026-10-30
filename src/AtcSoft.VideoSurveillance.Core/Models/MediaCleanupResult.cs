namespace AtcSoft.VideoSurveillance.Models;

/// <summary>
/// Result of a media cleanup operation.
/// </summary>
public class MediaCleanupResult
{
    /// <summary>
    /// Gets or sets the number of recording files deleted.
    /// </summary>
    public int RecordingsDeleted { get; set; }

    /// <summary>
    /// Gets or sets the number of snapshot files deleted.
    /// </summary>
    public int SnapshotsDeleted { get; set; }

    /// <summary>
    /// Gets or sets the number of thumbnail files deleted.
    /// </summary>
    public int ThumbnailsDeleted { get; set; }

    /// <summary>
    /// Gets or sets the total bytes freed.
    /// </summary>
    public long BytesFreed { get; set; }

    /// <summary>
    /// Gets or sets the number of errors encountered during cleanup.
    /// </summary>
    public int ErrorCount { get; set; }

    /// <summary>
    /// Gets or sets the number of empty directories removed.
    /// </summary>
    public int DirectoriesRemoved { get; set; }

    /// <summary>
    /// Gets the total number of files deleted.
    /// </summary>
    public int TotalFilesDeleted
        => RecordingsDeleted + SnapshotsDeleted + ThumbnailsDeleted;

    /// <summary>
    /// Gets a value indicating whether any files were deleted.
    /// </summary>
    public bool HasChanges => TotalFilesDeleted > 0 || DirectoriesRemoved > 0;

    /// <inheritdoc />
    public override string ToString()
        => $"MediaCleanupResult {{ FilesDeleted={TotalFilesDeleted.ToString(CultureInfo.InvariantCulture)}, BytesFreed={BytesFreed.ToString(CultureInfo.InvariantCulture)}, Errors={ErrorCount.ToString(CultureInfo.InvariantCulture)} }}";
}