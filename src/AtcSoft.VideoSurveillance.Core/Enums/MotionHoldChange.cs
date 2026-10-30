namespace AtcSoft.VideoSurveillance.Enums;

/// <summary>
/// What a picture changed about a camera's motion.
/// </summary>
public enum MotionHoldChange
{
    /// <summary>Nothing to report.</summary>
    None,

    /// <summary>Motion began.</summary>
    Started,

    /// <summary>Motion goes on, with new boxes.</summary>
    Continues,

    /// <summary>The post-motion time passed without movement: motion ended.</summary>
    Ended,
}