namespace AtcSoft.VideoSurveillance.Models;

/// <summary>
/// Something that moved on a camera's timeline, to the second, and where to play it.
/// </summary>
/// <param name="Start">When it started, in UTC.</param>
/// <param name="End">When it ended, in UTC.</param>
/// <param name="PeakLevel">The highest level in it, 0 to 100.</param>
/// <param name="Tracks">The most moving things at once in it.</param>
/// <param name="VideoPath">The recording it starts in.</param>
/// <param name="OffsetSeconds">Where in that recording it starts, in seconds.</param>
public sealed record TimelineActivityEvent(
    DateTimeOffset Start,
    DateTimeOffset End,
    double PeakLevel,
    int Tracks,
    string VideoPath,
    double OffsetSeconds);