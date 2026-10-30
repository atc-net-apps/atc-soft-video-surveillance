namespace AtcSoft.VideoSurveillance.Wpf.Core.Services;

/// <summary>
/// Loads the preview pictures of a recording for the timeline under its player: CameraWall reads
/// them beside the file (<see cref="RecordingPreviewLoaders.FromFile"/>); the server client asks
/// the server.
/// </summary>
/// <param name="cancellationToken">Stops the loading.</param>
/// <returns>The pictures, or that there are none (yet).</returns>
public delegate Task<RecordingPreviewLoad> RecordingPreviewLoader(
    CancellationToken cancellationToken);