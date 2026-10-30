namespace AtcSoft.VideoSurveillance.Wpf.Core.Services;

/// <summary>
/// The ways a recording's activity is loaded.
/// </summary>
public static class RecordingActivityLoaders
{
    /// <summary>
    /// Reads the activity index written beside a recording on this machine.
    /// </summary>
    /// <param name="videoPath">The recording.</param>
    /// <returns>The loader.</returns>
    public static RecordingActivityLoader FromFile(string videoPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(videoPath);

        return (buckets, durationSeconds, cancellationToken) => Task.Run<IReadOnlyList<ActivityBucket>?>(
            () => ActivityLogReader.Read(RecordingPathHelper.BuildActivityPath(videoPath)) is { } log
                ? ActivityDownsampler.ByMax(log, durationSeconds, buckets)
                : null,
            cancellationToken);
    }
}