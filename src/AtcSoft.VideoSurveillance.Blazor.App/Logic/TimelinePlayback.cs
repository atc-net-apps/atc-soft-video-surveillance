namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// Where a moment on the Timeline page plays: the recording that covers it, and how far into it.
/// </summary>
/// <param name="FilePath">The recording, as the listing names it.</param>
/// <param name="OffsetSeconds">How far into it, in seconds.</param>
public sealed record TimelinePlayback(
    string FilePath,
    double OffsetSeconds)
{
    /// <summary>
    /// Finds the recording that covers a moment of a lane.
    /// </summary>
    /// <param name="lane">The camera's lane.</param>
    /// <param name="moment">The moment.</param>
    /// <returns>Where to play, or <see langword="null"/> when nothing was recorded then.</returns>
    public static TimelinePlayback? At(
        TimelineLane lane,
        DateTimeOffset moment)
    {
        ArgumentNullException.ThrowIfNull(lane);

        return lane.Coverage.FirstOrDefault(c => c.Start <= moment && moment < c.End) is { } covering
            ? new TimelinePlayback(covering.FilePath, Math.Max((moment - covering.RecordingStart).TotalSeconds, 0))
            : null;
    }

    /// <summary>
    /// Gets the lane's recordings that follow one, in time order: what plays after it, gaps skipped.
    /// </summary>
    /// <param name="lane">The camera's lane.</param>
    /// <param name="filePath">The recording that plays first.</param>
    /// <returns>The following recordings; none when the recording is not in the lane.</returns>
    public static IReadOnlyList<TimelineCoverage> Following(
        TimelineLane lane,
        string filePath)
    {
        ArgumentNullException.ThrowIfNull(lane);

        return [.. lane.Coverage
            .OrderBy(c => c.Start)
            .SkipWhile(c => !string.Equals(c.FilePath, filePath, StringComparison.Ordinal))
            .Skip(1)];
    }

    /// <summary>
    /// Gets where an event plays from: a second before it starts, so its start is seen.
    /// </summary>
    /// <param name="activityEvent">The event.</param>
    public static TimelinePlayback Of(TimelineEvent activityEvent)
    {
        ArgumentNullException.ThrowIfNull(activityEvent);

        return new TimelinePlayback(activityEvent.FilePath, Math.Max(activityEvent.OffsetSeconds - ActivityEventJumps.LeadInSeconds, 0));
    }
}