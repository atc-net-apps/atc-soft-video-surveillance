namespace AtcSoft.VideoSurveillance.Storybook.Truth;

/// <summary>
/// What a detector should do about something in a frame.
/// </summary>
public enum TruthState
{
    /// <summary>A moving object: a box must be on it.</summary>
    MustBox,

    /// <summary>A distractor: no box may be on it.</summary>
    MustNotBox,

    /// <summary>Either is fine: standing still, entering, a lighting step.</summary>
    DontCare,
}