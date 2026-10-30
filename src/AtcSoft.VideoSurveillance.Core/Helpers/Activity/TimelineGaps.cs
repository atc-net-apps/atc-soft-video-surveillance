namespace AtcSoft.VideoSurveillance.Helpers.Activity;

/// <summary>
/// Finds the stretches of a camera's timeline without a recording, and explains each from the
/// history of whether the camera could be recorded: the state at the gap's start decides, and a
/// gap whose reason changes on the way (back online, but the disk is full) is cut in two there.
/// </summary>
/// <remarks>
/// The history is replayed once for all gaps, so a camera that went on and off thousands of times
/// costs no more than reading its history.
/// </remarks>
public static class TimelineGaps
{
    // A second of slack: the recording's end and the change that ended it are noted apart.
    private static readonly TimeSpan Slack = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Finds the gaps of a span.
    /// </summary>
    /// <param name="coverage">Where recordings cover the span, cut to it.</param>
    /// <param name="from">The start of the span.</param>
    /// <param name="to">The end of the span.</param>
    /// <param name="history">The camera's changes and the server's starts and stops, in any order.</param>
    /// <returns>The gaps, from the start of the span.</returns>
    public static IReadOnlyList<TimelineGap> Find(
        IReadOnlyList<CoverageSpan> coverage,
        DateTimeOffset from,
        DateTimeOffset to,
        IReadOnlyList<AvailabilityEntry> history)
    {
        ArgumentNullException.ThrowIfNull(coverage);
        ArgumentNullException.ThrowIfNull(history);

        var ordered = history.OrderBy(e => e.Time).ToList();
        var replay = new Replay(ordered);
        var gaps = new List<TimelineGap>();
        var cursor = from;
        foreach (var span in coverage.OrderBy(c => c.Start))
        {
            if (span.Start > cursor)
            {
                AddExplained(gaps, replay, cursor, span.Start);
            }

            if (span.End > cursor)
            {
                cursor = span.End;
            }
        }

        if (cursor < to)
        {
            AddExplained(gaps, replay, cursor, to);
        }

        return gaps;
    }

    /// <summary>
    /// Explains why nothing was recorded from a moment: the latest state known then.
    /// </summary>
    /// <param name="history">The changes, oldest first.</param>
    /// <param name="moment">The start of the gap.</param>
    public static GapReason Explain(
        IReadOnlyList<AvailabilityEntry> history,
        DateTimeOffset moment)
    {
        ArgumentNullException.ThrowIfNull(history);

        var replay = new Replay(history);
        replay.ApplyUntil(moment + Slack);
        return replay.Reason;
    }

    private static void AddExplained(
        List<TimelineGap> gaps,
        Replay replay,
        DateTimeOffset start,
        DateTimeOffset end)
    {
        replay.ApplyUntil(start + Slack);
        var reason = replay.Reason;
        while (replay.Next is { } change && change < end)
        {
            replay.ApplyUntil(change + Slack);
            if (replay.Reason != reason)
            {
                gaps.Add(new TimelineGap(start, change, reason));
                start = change;
                reason = replay.Reason;
            }
        }

        gaps.Add(new TimelineGap(start, end, reason));
    }

    // The state of the history, moved forward only.
    private sealed class Replay(IReadOnlyList<AvailabilityEntry> ordered)
    {
        private int index;
        private bool known;
        private bool offline;
        private bool serverDown;
        private bool diskFull;

        public DateTimeOffset? Next
            => index < ordered.Count ? ordered[index].Time : null;

        public GapReason Reason
            => !known ? GapReason.Unknown
                : serverDown ? GapReason.ServerDown
                : offline ? GapReason.Offline
                : diskFull ? GapReason.DiskFull
                : GapReason.NotRecording;

        public void ApplyUntil(DateTimeOffset moment)
        {
            for (; index < ordered.Count && ordered[index].Time <= moment; index++)
            {
                known = true;
                switch (ordered[index].Change)
                {
                    case AvailabilityChange.Online:
                        offline = false;
                        break;
                    case AvailabilityChange.Offline:
                        offline = true;
                        break;
                    case AvailabilityChange.RecordingStarted:
                        diskFull = false;
                        break;
                    case AvailabilityChange.DiskFull:
                        diskFull = true;
                        break;
                    case AvailabilityChange.ServerStarted:
                        serverDown = false;
                        offline = false;
                        break;
                    case AvailabilityChange.ServerStopped:
                        serverDown = true;
                        break;
                }
            }
        }
    }
}