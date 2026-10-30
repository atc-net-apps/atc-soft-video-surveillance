namespace AtcSoft.VideoSurveillance.Models.Settings;

/// <summary>
/// Settings for automatic cleanup of old recordings and snapshots.
/// </summary>
public class MediaCleanupSettings
{
    public MediaCleanupSchedule Schedule { get; set; } = MediaCleanupSchedule.OnStartupAndPeriodically;

    public int RecordingRetentionDays { get; set; } = 30;

    public bool IncludeSnapshots { get; set; }

    public int SnapshotRetentionDays { get; set; } = 7;

    /// <summary>
    /// Gets or sets a value indicating whether the disk space guard is active.
    /// When <c>true</c>, an oldest-first reclaim pass fires at recording start
    /// and segment rollover when available drive space drops below
    /// <see cref="MinFreeSpaceMb"/>.
    /// </summary>
    public bool EnableDiskSpaceGuard { get; set; } = true;

    /// <summary>
    /// Gets or sets the minimum free disk space in megabytes on the recording
    /// drive. The guard fires a reclaim pass when available free space drops
    /// below this value. Default 2048 MB (2 GB). Recommended minimum for
    /// multi-camera 24/7 recording.
    /// </summary>
    public int MinFreeSpaceMb { get; set; } = 2048;

    /// <summary>
    /// Gets or sets the most space, in gigabytes, all recordings together may take; the oldest
    /// go first when they take more. 0 (the default) is no limit: the age and the free space
    /// kept decide alone.
    /// </summary>
    public int MaxRecordingStorageGb { get; set; }

    /// <inheritdoc />
    public override string ToString()
        => $"MediaCleanupSettings {{ Schedule={Schedule}, RetentionDays={RecordingRetentionDays.ToString(CultureInfo.InvariantCulture)}, MaxStorageGb={MaxRecordingStorageGb.ToString(CultureInfo.InvariantCulture)} }}";
}