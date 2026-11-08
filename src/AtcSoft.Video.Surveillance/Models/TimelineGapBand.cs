namespace AtcSoft.Video.Surveillance.Models;

/// <summary>
/// A gap of a camera's timeline as the lane control draws it: where it starts and ends, as
/// fractions of the span, and why nothing was recorded.
/// </summary>
/// <param name="Start">Where the gap starts, 0 to 1.</param>
/// <param name="End">Where the gap ends, 0 to 1.</param>
/// <param name="Reason">Why nothing was recorded.</param>
/// <param name="Text">What the pointer on the gap tells; the reason's text when not given.</param>
public sealed record TimelineGapBand(
    double Start,
    double End,
    GapReason Reason,
    string? Text = null);