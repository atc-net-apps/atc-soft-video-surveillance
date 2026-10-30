namespace AtcSoft.VideoSurveillance.Helpers.Activity;

/// <summary>
/// Builds a camera's timeline over hours or days from its recordings and their activity
/// summaries: coverage from the video files, stretches with the most of each (never the
/// average), and events to the second, joined across segments. Pure, so CameraWall can use it
/// over its own folders too.
/// </summary>
public static class TimelineBuilder
{
    /// <summary>
    /// Builds a camera's timeline over the span.
    /// </summary>
    /// <param name="recordings">The camera's recordings, in any order.</param>
    /// <param name="from">The start of the span.</param>
    /// <param name="to">The end of the span, after <paramref name="from"/>.</param>
    /// <param name="stretches">How many equal stretches, at least 1.</param>
    /// <returns>The timeline.</returns>
    public static CameraTimeline Build(
        IReadOnlyList<TimelineRecording> recordings,
        DateTimeOffset from,
        DateTimeOffset to,
        int stretches)
    {
        ArgumentNullException.ThrowIfNull(recordings);
        ArgumentOutOfRangeException.ThrowIfLessThan(stretches, 1);
        if (to <= from)
        {
            throw new ArgumentOutOfRangeException(nameof(to), "The span must end after it starts.");
        }

        var span = (to - from).TotalSeconds;
        var stretchSeconds = span / stretches;
        var level = new double[stretches];
        var tracks = new int[stretches];
        var recorded = new bool[stretches];
        var analysed = new bool[stretches];
        var coverage = new List<CoverageSpan>();

        foreach (var recording in recordings.OrderBy(r => r.Start))
        {
            var start = Max(recording.Start, from);
            var end = Min(recording.End, to);
            if (end <= start)
            {
                continue;
            }

            coverage.Add(new CoverageSpan(start, end, recording.VideoPath, recording.Start));
            ForEachStretch((start - from).TotalSeconds, (end - from).TotalSeconds, i => recorded[i] = true);

            if (recording.Activity is not { } activity)
            {
                continue;
            }

            for (var b = 0; b < activity.Buckets.Count; b++)
            {
                var bucket = activity.Buckets[b];
                if (!bucket.IsAnalysed)
                {
                    continue;
                }

                var bucketStart = (activity.Start - from).TotalSeconds + (b * ActivitySummarizer.BucketSeconds);
                var bucketEnd = Math.Min(bucketStart + ActivitySummarizer.BucketSeconds, (activity.Start - from).TotalSeconds + activity.DurationSeconds);
                ForEachStretch(bucketStart, bucketEnd, i =>
                {
                    level[i] = Math.Max(level[i], bucket.Level);
                    tracks[i] = Math.Max(tracks[i], bucket.Tracks);
                    analysed[i] = true;
                });
            }
        }

        var result = new TimelineStretch[stretches];
        for (var i = 0; i < stretches; i++)
        {
            result[i] = new TimelineStretch(level[i], tracks[i], recorded[i], analysed[i]);
        }

        var events = FindEvents(recordings)
            .Where(e => e.End > from && e.Start < to)
            .ToList();

        return new CameraTimeline(coverage, result, events);

        // A stretch is touched by anything that overlaps it; a short thing in a long stretch still marks it.
        void ForEachStretch(
            double startSeconds,
            double endSeconds,
            Action<int> mark)
        {
            if (endSeconds <= 0 || startSeconds >= span || endSeconds <= startSeconds)
            {
                return;
            }

            var first = (int)Math.Floor(Math.Max(startSeconds, 0) / stretchSeconds);
            var last = (int)Math.Ceiling(Math.Min(endSeconds, span) / stretchSeconds) - 1;
            for (var i = Math.Max(first, 0); i <= Math.Min(last, stretches - 1); i++)
            {
                mark(i);
            }
        }
    }

    /// <summary>
    /// Finds every event of a camera's recordings, to the second, with moving seconds
    /// <see cref="ActivitySummarizer.MergeGapSeconds"/> or less apart joined, also across segments.
    /// </summary>
    /// <param name="recordings">The camera's recordings, in any order.</param>
    /// <returns>The events, from the earliest.</returns>
    public static IReadOnlyList<TimelineActivityEvent> FindEvents(
        IReadOnlyList<TimelineRecording> recordings)
    {
        ArgumentNullException.ThrowIfNull(recordings);

        var events = new List<TimelineActivityEvent>();
        var pieces = recordings
            .Where(r => r.Activity is not null)
            .SelectMany(r => r.Activity!.Events.Select(e => (Recording: r, Span: e, Start: r.Activity.Start.AddSeconds(e.StartOffset))))
            .OrderBy(p => p.Start);

        TimelineActivityEvent? open = null;
        foreach (var (recording, span, start) in pieces)
        {
            var end = recording.Activity!.Start.AddSeconds(span.EndOffset);
            if (open is not null && (start - open.End).TotalSeconds <= ActivitySummarizer.MergeGapSeconds)
            {
                open = open with
                {
                    End = Max(open.End, end),
                    PeakLevel = Math.Max(open.PeakLevel, span.PeakLevel),
                    Tracks = Math.Max(open.Tracks, span.Tracks),
                };
                continue;
            }

            if (open is not null)
            {
                events.Add(open);
            }

            open = new TimelineActivityEvent(
                start,
                end,
                span.PeakLevel,
                span.Tracks,
                recording.VideoPath,
                Math.Max((start - recording.Start).TotalSeconds, 0));
        }

        if (open is not null)
        {
            events.Add(open);
        }

        return events;
    }

    private static DateTimeOffset Max(
        DateTimeOffset a,
        DateTimeOffset b)
        => a > b ? a : b;

    private static DateTimeOffset Min(
        DateTimeOffset a,
        DateTimeOffset b)
        => a < b ? a : b;
}