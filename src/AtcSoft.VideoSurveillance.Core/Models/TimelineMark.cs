namespace AtcSoft.VideoSurveillance.Models;

/// <summary>
/// A mark under a timeline of every camera.
/// </summary>
/// <param name="Fraction">Where it is, 0 to 1 of the span.</param>
/// <param name="Label">What it says: a time, or a day for a span of days.</param>
public sealed record TimelineMark(
    double Fraction,
    string Label);