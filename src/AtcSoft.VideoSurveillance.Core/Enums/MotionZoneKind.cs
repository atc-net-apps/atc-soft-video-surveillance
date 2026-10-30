namespace AtcSoft.VideoSurveillance.Enums;

/// <summary>
/// What a motion zone of a camera does.
/// </summary>
public enum MotionZoneKind
{
    /// <summary>Nothing inside is looked at: a tree, a road, a neighbour's window.</summary>
    Ignore = 0,

    /// <summary>Watched with its own sensitivity and smallest object.</summary>
    Watch = 1,
}