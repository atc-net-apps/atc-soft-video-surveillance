namespace AtcSoft.VideoSurveillance.Models;

/// <summary>
/// A stretch of one recording in which something moved (a confirmed track), to the second.
/// </summary>
/// <param name="StartOffset">Seconds from the start of the recording's index.</param>
/// <param name="EndOffset">Seconds from the start of the index to the end of its last second.</param>
/// <param name="PeakLevel">The highest level in it, 0 to 100.</param>
/// <param name="Tracks">The most moving things at once in it.</param>
public sealed record ActivitySpan(
    int StartOffset,
    int EndOffset,
    double PeakLevel,
    int Tracks);