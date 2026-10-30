namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// A rectangle of the activity timeline's drawing, in its box (one unit per stretch wide,
/// <see cref="ActivityTimelineShapes.BoxHeight"/> high).
/// </summary>
public sealed record TimelineRect(
    double X,
    double Y,
    double Width,
    double Height);