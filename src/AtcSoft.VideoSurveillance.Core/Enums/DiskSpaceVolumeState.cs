namespace AtcSoft.VideoSurveillance.Enums;

/// <summary>
/// What the disk-space guard found on a volume.
/// </summary>
public enum DiskSpaceVolumeState
{
    /// <summary>The volume has the free space the guard keeps.</summary>
    Ok,

    /// <summary>The volume was short, and old media was deleted until it was not.</summary>
    Reclaimed,

    /// <summary>The volume is short, and there is nothing more the guard may delete.</summary>
    StillShort,

    /// <summary>The volume's free space could not be read, such as a network share that is gone.</summary>
    Unreadable,
}