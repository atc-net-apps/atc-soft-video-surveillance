namespace AtcSoft.VideoSurveillance.Models;

/// <summary>
/// One stretch of a camera's timeline: whether it was recorded, whether what was recorded was
/// analysed, and the most that happened in it.
/// </summary>
/// <param name="Level">The highest level, 0 to 100.</param>
/// <param name="Tracks">The most moving things at once.</param>
/// <param name="IsRecorded">Whether any recording covers the stretch; a stretch without one is a gap.</param>
/// <param name="IsAnalysed">Whether any of it was analysed; recorded but not analysed is unknown, not quiet.</param>
public sealed record TimelineStretch(
    double Level,
    int Tracks,
    bool IsRecorded,
    bool IsAnalysed);