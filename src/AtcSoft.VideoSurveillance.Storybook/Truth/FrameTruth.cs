namespace AtcSoft.VideoSurveillance.Storybook.Truth;

/// <summary>
/// What a frame of a scene holds: where every object and distractor was drawn, and whether the
/// whole frame is to be ignored (a lighting step).
/// </summary>
/// <param name="Index">The frame, from 0.</param>
/// <param name="Time">Its time, in seconds from the start.</param>
/// <param name="Truth">The objects and distractors drawn in it.</param>
/// <param name="IgnoreAll">Whether nothing in the frame is scored.</param>
public sealed record FrameTruth(
    int Index,
    double Time,
    IReadOnlyList<TruthBox> Truth,
    bool IgnoreAll);