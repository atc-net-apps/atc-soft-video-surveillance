namespace AtcSoft.VideoSurveillance.Enums;

/// <summary>
/// What the pacer of *play all from here* asks a tile to do.
/// </summary>
public enum PlayAllAction
{
    /// <summary>Go on as it is.</summary>
    None,

    /// <summary>Pause: it is ahead of the others.</summary>
    Hold,

    /// <summary>Play again: the others caught up.</summary>
    Resume,

    /// <summary>Open its next recording at the pacer's clock: the clock reached it.</summary>
    Start,

    /// <summary>Stop trying: it loaded too long. Show it as unavailable.</summary>
    GiveUp,
}