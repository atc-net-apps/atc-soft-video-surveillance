namespace AtcSoft.VideoSurveillance.Wpf.Core.Services;

/// <summary>
/// Saves a verdict given on a recording's timeline in the review beside it. CameraWall writes the
/// file (<see cref="RecordingVerdictSavers"/>); the server client asks the server.
/// </summary>
/// <param name="verdict">The verdict and the stretch it is about.</param>
/// <param name="cancellationToken">Cancels the saving.</param>
/// <returns>A task that ends when it is saved; it fails with the reason when it is not.</returns>
public delegate Task RecordingVerdictSaver(
    TimelineVerdict verdict,
    CancellationToken cancellationToken);