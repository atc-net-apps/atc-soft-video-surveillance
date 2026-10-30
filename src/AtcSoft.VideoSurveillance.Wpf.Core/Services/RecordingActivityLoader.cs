namespace AtcSoft.VideoSurveillance.Wpf.Core.Services;

/// <summary>
/// Gets a recording's activity for its timeline: the recording split into equal stretches, each
/// with the most that happened in it. CameraWall reads the index beside the file
/// (<see cref="RecordingActivityLoaders.FromFile"/>); the server client asks the server.
/// </summary>
/// <param name="buckets">How many stretches.</param>
/// <param name="durationSeconds">How long the recording is, in seconds.</param>
/// <param name="cancellationToken">Cancels the loading.</param>
/// <returns>The stretches, or <see langword="null"/> when the recording has no activity index.</returns>
public delegate Task<IReadOnlyList<ActivityBucket>?> RecordingActivityLoader(
    int buckets,
    double durationSeconds,
    CancellationToken cancellationToken);