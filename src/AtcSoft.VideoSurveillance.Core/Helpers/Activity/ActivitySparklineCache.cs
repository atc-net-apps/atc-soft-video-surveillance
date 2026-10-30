namespace AtcSoft.VideoSurveillance.Helpers.Activity;

/// <summary>
/// Keeps the sparklines of the recordings a list has shown. A sparkline is 24 numbers, so those
/// of a whole archive are kept, and a list does not push the summaries the timelines use out of
/// their cache. A sparkline is read again when its index changed.
/// </summary>
public sealed class ActivitySparklineCache
{
    private readonly ActivityFileCache<IReadOnlyList<int>> files;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivitySparklineCache"/> class.
    /// </summary>
    /// <param name="capacity">How many sparklines are kept at most.</param>
    public ActivitySparklineCache(int capacity = 100_000)
        => files = new ActivityFileCache<IReadOnlyList<int>>(
            capacity,
            path => ActivitySparkline.From(ActivityLogReader.Read(path) is { } log ? ActivitySummarizer.Summarize(log) : null));

    /// <summary>
    /// Gets how many sparklines are kept.
    /// </summary>
    public int Count
        => files.Count;

    /// <summary>
    /// Gets the sparkline of the recording whose activity index is at the path.
    /// </summary>
    /// <param name="activityPath">The index, from <see cref="RecordingPathHelper.BuildActivityPath"/>.</param>
    /// <returns>The sparkline, or <see langword="null"/> when there is no readable index.</returns>
    public IReadOnlyList<int>? Get(string activityPath)
        => files.Get(activityPath);
}