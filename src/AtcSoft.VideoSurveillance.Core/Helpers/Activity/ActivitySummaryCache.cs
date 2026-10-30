namespace AtcSoft.VideoSurveillance.Helpers.Activity;

/// <summary>
/// Keeps the summaries of activity indexes that timelines have read, so a timeline over days
/// reads each index once. A summary is read again when its file changed (size or time, as an
/// index grows while recording); the least recently used are dropped beyond the limit.
/// </summary>
public sealed class ActivitySummaryCache
{
    private readonly ActivityFileCache<ActivitySummary> files;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivitySummaryCache"/> class.
    /// </summary>
    /// <param name="capacity">How many summaries are kept at most.</param>
    public ActivitySummaryCache(int capacity = 5000)
        => files = new ActivityFileCache<ActivitySummary>(
            capacity,
            path => ActivityLogReader.Read(path) is { } log ? ActivitySummarizer.Summarize(log) : null);

    /// <summary>
    /// Gets how many summaries are kept.
    /// </summary>
    public int Count
        => files.Count;

    /// <summary>
    /// Gets the summary of the activity index at the path.
    /// </summary>
    /// <param name="activityPath">The index, from <see cref="RecordingPathHelper.BuildActivityPath"/>.</param>
    /// <returns>The summary, or <see langword="null"/> when there is no readable index.</returns>
    public ActivitySummary? Get(string activityPath)
        => files.Get(activityPath);
}