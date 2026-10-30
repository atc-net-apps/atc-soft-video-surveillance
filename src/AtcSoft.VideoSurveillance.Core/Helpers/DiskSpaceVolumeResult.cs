namespace AtcSoft.VideoSurveillance.Helpers;

/// <summary>
/// What the disk-space guard found and did on one volume.
/// </summary>
/// <param name="Volume">The volume, or the folder when its volume could not be found.</param>
/// <param name="Folders">The media folders on it.</param>
/// <param name="State">What the guard found.</param>
/// <param name="FreeBytes">The free space before the guard deleted anything, when it could be read.</param>
/// <param name="Run">What was deleted.</param>
/// <param name="StateChanged">Whether the state differs from the guard's previous pass, so a problem is reported once, not every pass.</param>
public sealed record DiskSpaceVolumeResult(
    string Volume,
    IReadOnlyList<string> Folders,
    DiskSpaceVolumeState State,
    long? FreeBytes,
    MediaCleanupRunResult Run,
    bool StateChanged = true);