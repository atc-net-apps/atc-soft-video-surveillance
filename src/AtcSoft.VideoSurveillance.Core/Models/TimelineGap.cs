namespace AtcSoft.VideoSurveillance.Models;

/// <summary>
/// A stretch of a camera's timeline without a recording, and why.
/// </summary>
/// <param name="Start">The start, in UTC.</param>
/// <param name="End">The end, in UTC.</param>
/// <param name="Reason">Why nothing was recorded.</param>
public sealed record TimelineGap(
    DateTimeOffset Start,
    DateTimeOffset End,
    GapReason Reason);