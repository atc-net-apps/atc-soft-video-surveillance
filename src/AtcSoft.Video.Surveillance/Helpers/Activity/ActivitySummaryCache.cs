namespace AtcSoft.Video.Surveillance.Helpers.Activity;

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
    /// <param name="capacity">How many summaries are kept at most, until a timeline needs more.</param>
    /// <param name="maximumCapacity">
    /// How far <see cref="Reserve"/> may grow the cache. A summary of an hour is about 17 KB, so
    /// the default of 50,000 is a week of hourly recordings of some 300 cameras.
    /// </param>
    public ActivitySummaryCache(
        int capacity = 5000,
        int maximumCapacity = 50_000)
        => files = new ActivityFileCache<ActivitySummary>(
            capacity,
            path => ActivityLogReader.Read(path) is { } log ? ActivitySummarizer.Summarize(log) : null,
            maximumCapacity);

    /// <summary>
    /// Gets how many summaries are kept.
    /// </summary>
    public int Count
        => files.Count;

    /// <summary>
    /// Gets how many summaries are kept at most now.
    /// </summary>
    public int Capacity
        => files.Capacity;

    /// <summary>
    /// Makes room for a timeline that reads <paramref name="count"/> summaries, so a timeline
    /// built again finds them all kept (up to the maximum capacity). The cache never shrinks.
    /// </summary>
    /// <param name="count">How many summaries the timeline reads.</param>
    public void Reserve(int count)
        => files.Reserve(count);

    /// <summary>
    /// Gets the summary of the activity index at the path.
    /// </summary>
    /// <param name="activityPath">The index, from <see cref="RecordingPathHelper.BuildActivityPath"/>.</param>
    /// <returns>The summary, or <see langword="null"/> when there is no readable index.</returns>
    public ActivitySummary? Get(string activityPath)
        => files.Get(activityPath);
}