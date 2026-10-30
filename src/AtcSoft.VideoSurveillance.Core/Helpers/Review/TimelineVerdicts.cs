namespace AtcSoft.VideoSurveillance.Helpers.Review;

/// <summary>
/// What a right-click on a recording's activity timeline offers: on an event's band, that it was
/// boxed as it should be or was a false alarm, over the whole event; anywhere, that something was
/// missed there, over a few seconds around the place. The web client keeps a copy in its
/// <c>Logic/</c>, held equal by a test.
/// </summary>
public static class TimelineVerdicts
{
    /// <summary>
    /// How long a stretch marked as missed around a place is, in seconds.
    /// </summary>
    public const double MissedSeconds = 5;

    /// <summary>
    /// Gets the verdicts offered at a place on the timeline.
    /// </summary>
    /// <param name="events">The recording's events, as fractions of it.</param>
    /// <param name="place">The place clicked, as a fraction of the recording.</param>
    /// <param name="durationSeconds">How long the recording is.</param>
    /// <param name="slack">How far beside a band still counts as on it, as a fraction of the recording (a band is drawn at least a few pixels wide).</param>
    /// <returns>On an event: correct and false box over it, then missed; elsewhere only missed. None for a recording of no length.</returns>
    public static IReadOnlyList<TimelineVerdict> At(
        IReadOnlyList<ActivityEvent> events,
        double place,
        double durationSeconds,
        double slack = 0)
    {
        ArgumentNullException.ThrowIfNull(events);

        if (durationSeconds <= 0)
        {
            return [];
        }

        place = Math.Clamp(place, 0, 1);
        var verdicts = new List<TimelineVerdict>();
        if (events.FirstOrDefault(e => place >= e.Start - slack && place <= e.End + slack) is { } hit)
        {
            var from = hit.Start * durationSeconds;
            var to = Math.Max(hit.End * durationSeconds, from + 0.001);
            verdicts.Add(new TimelineVerdict(ReviewVerdicts.Correct, from, to));
            verdicts.Add(new TimelineVerdict(ReviewVerdicts.FalseBox, from, to));
        }

        var at = place * durationSeconds;
        var half = Math.Min(MissedSeconds, durationSeconds) / 2;
        var start = Math.Clamp(at - half, 0, durationSeconds - (2 * half));
        verdicts.Add(new TimelineVerdict(ReviewVerdicts.Missed, start, start + (2 * half)));
        return verdicts;
    }
}