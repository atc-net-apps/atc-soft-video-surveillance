namespace AtcSoft.VideoSurveillance.Storybook.Truth;

/// <summary>
/// Where something was drawn in a frame, and what a detector should do about it.
/// </summary>
/// <param name="Id">The object's or distractor's id.</param>
/// <param name="Box">The box of the pixels actually drawn, clipped to the frame.</param>
/// <param name="State">What a detector should do about it.</param>
/// <param name="IsDistractor">Whether it is a distractor rather than an object.</param>
public sealed record TruthBox(
    string Id,
    PixelBox Box,
    TruthState State,
    bool IsDistractor);