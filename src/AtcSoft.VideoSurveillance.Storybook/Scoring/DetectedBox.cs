namespace AtcSoft.VideoSurveillance.Storybook.Scoring;

/// <summary>
/// A box a detector showed, in the frame's pixels.
/// </summary>
/// <param name="Box">The box.</param>
/// <param name="TrackId">The detector's track id, when it gives one.</param>
public sealed record DetectedBox(
    PixelBox Box,
    int? TrackId = null);