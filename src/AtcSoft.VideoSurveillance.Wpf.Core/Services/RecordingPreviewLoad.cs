namespace AtcSoft.VideoSurveillance.Wpf.Core.Services;

/// <summary>
/// The preview pictures of a recording as loaded, or that it has none yet.
/// </summary>
/// <param name="Sheet">The pictures, or <see langword="null"/> when there are none.</param>
/// <param name="IsBeingMade">Whether they are queued or being made, so asking again later finds them.</param>
public sealed record RecordingPreviewLoad(
    RecordingPreviewTiles? Sheet,
    bool IsBeingMade);