namespace AtcSoft.VideoSurveillance.Wpf.Core.Windows;

/// <summary>
/// A recording one tile of *play all from here* plays.
/// </summary>
/// <param name="Address">The file or address to play.</param>
/// <param name="Start">Where the recording starts on the timeline; the tile waits until the clock gets there.</param>
/// <param name="RecordingStart">When the recording itself started: position 0 of the file.</param>
public sealed record PlayAllSegment(
    Uri Address,
    DateTimeOffset Start,
    DateTimeOffset RecordingStart);