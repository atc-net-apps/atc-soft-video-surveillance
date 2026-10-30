namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// A recording one tile plays.
/// </summary>
/// <param name="Source">The address the video element opens.</param>
/// <param name="Start">Where it starts on the timeline; the tile waits until the clock gets there.</param>
/// <param name="RecordingStart">When the recording itself started: second 0 of the file.</param>
public sealed record PlayAllSegment(
    string Source,
    DateTimeOffset Start,
    DateTimeOffset RecordingStart);