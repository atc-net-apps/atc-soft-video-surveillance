namespace AtcSoft.VideoSurveillance.Models;

/// <summary>
/// A corner of a motion zone, in fractions of the picture as it is shown.
/// </summary>
/// <param name="X">From the left edge, 0 to 1.</param>
/// <param name="Y">From the top edge, 0 to 1.</param>
public sealed record ZonePoint(
    double X,
    double Y);