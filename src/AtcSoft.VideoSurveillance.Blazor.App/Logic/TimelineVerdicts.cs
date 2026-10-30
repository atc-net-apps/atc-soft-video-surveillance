namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// What a right-click on the web player's activity timeline offers: on an event's band, that it
/// was boxed correctly or was a false alarm, over the whole event; anywhere, that something was
/// missed there, over a few seconds around the place. A copy of Core's <c>TimelineVerdicts</c>,
/// held equal by a test; the words are the desktop apps'.
/// </summary>
public static class TimelineVerdicts
{
    /// <summary>
    /// Boxed as it should be.
    /// </summary>
    public const string Correct = "correct";

    /// <summary>
    /// Boxed, but nothing that matters moves.
    /// </summary>
    public const string FalseBox = "falseBox";

    /// <summary>
    /// Something moved and was not boxed.
    /// </summary>
    public const string Missed = "missed";

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
    /// <param name="slack">How far beside a band still counts as on it, as a fraction of the recording.</param>
    /// <returns>On an event: correct and false box over it, then missed; elsewhere only missed. None for a recording of no length.</returns>
    public static IReadOnlyList<TimelineVerdict> At(
        IReadOnlyList<ActivityTimelineEvent> events,
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
            verdicts.Add(new TimelineVerdict(Correct, from, to));
            verdicts.Add(new TimelineVerdict(FalseBox, from, to));
        }

        var at = place * durationSeconds;
        var half = Math.Min(MissedSeconds, durationSeconds) / 2;
        var start = Math.Clamp(at - half, 0, durationSeconds - (2 * half));
        verdicts.Add(new TimelineVerdict(Missed, start, start + (2 * half)));
        return verdicts;
    }

    /// <summary>
    /// Gets the menu's words for a verdict.
    /// </summary>
    /// <param name="verdict">The verdict.</param>
    public static string Text(string verdict)
        => verdict switch
        {
            Correct => Translations.VerdictCorrect,
            FalseBox => Translations.VerdictFalseAlarm,
            _ => Translations.VerdictMissed,
        };
}