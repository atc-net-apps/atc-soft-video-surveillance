namespace AtcSoft.VideoSurveillance.Models;

/// <summary>
/// The preview tile of an event: the moment most moved in it.
/// </summary>
/// <param name="Seconds">How far into the recording the moment is.</param>
/// <param name="Tile">The tile's number in the JPEG, after the grid's.</param>
public sealed record RecordingPreviewEvent(
    double Seconds,
    int Tile);