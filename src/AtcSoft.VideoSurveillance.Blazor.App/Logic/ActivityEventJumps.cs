namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// The web player's previous and next event, in seconds: a jump lands a second before the event,
/// so its start is seen, and "previous" right after a jump goes one further back. The same as
/// the desktop apps' player.
/// </summary>
public static class ActivityEventJumps
{
    /// <summary>
    /// How far before an event a jump lands, in seconds.
    /// </summary>
    public const double LeadInSeconds = 1;

    /// <summary>
    /// How long after an event's start "previous" still counts as at it, in seconds.
    /// </summary>
    public const double PreviousSlackSeconds = 2;

    /// <summary>
    /// How much after an event *play events only* shows, in seconds.
    /// </summary>
    public const double LeadOutSeconds = 2;

    /// <summary>
    /// Gets where a player that plays the events only should be.
    /// </summary>
    /// <param name="events">The events of the recording.</param>
    /// <param name="currentSeconds">Where the player is.</param>
    /// <param name="durationSeconds">How long the recording is.</param>
    /// <returns>
    /// Where to go, in seconds (the length for the end), or <see langword="null"/> to play on.
    /// </returns>
    public static double? EventsOnly(
        IReadOnlyList<ActivityTimelineEvent> events,
        double currentSeconds,
        double durationSeconds)
    {
        if (durationSeconds <= 0)
        {
            return null;
        }

        return ActivityTimelineLayout.EventsOnlyTarget(
            events,
            currentSeconds / durationSeconds,
            LeadInSeconds / durationSeconds,
            LeadOutSeconds / durationSeconds) is { } target
            ? target * durationSeconds
            : null;
    }

    /// <summary>
    /// Gets where the next event is.
    /// </summary>
    /// <param name="events">The events of the recording.</param>
    /// <param name="currentSeconds">Where the player is.</param>
    /// <param name="durationSeconds">How long the recording is.</param>
    /// <returns>Where to go, in seconds, or <see langword="null"/> when there is no next event.</returns>
    public static double? Next(
        IReadOnlyList<ActivityTimelineEvent> events,
        double currentSeconds,
        double durationSeconds)
    {
        if (durationSeconds <= 0)
        {
            return null;
        }

        return ActivityTimelineLayout.NextEventStart(events, (currentSeconds + LeadInSeconds) / durationSeconds) is { } start
            ? Math.Max((start * durationSeconds) - LeadInSeconds, 0)
            : null;
    }

    /// <summary>
    /// Gets where the previous event is.
    /// </summary>
    /// <param name="events">The events of the recording.</param>
    /// <param name="currentSeconds">Where the player is.</param>
    /// <param name="durationSeconds">How long the recording is.</param>
    /// <returns>Where to go, in seconds, or <see langword="null"/> when there is no previous event.</returns>
    public static double? Previous(
        IReadOnlyList<ActivityTimelineEvent> events,
        double currentSeconds,
        double durationSeconds)
    {
        if (durationSeconds <= 0)
        {
            return null;
        }

        var at = (currentSeconds + LeadInSeconds) / durationSeconds;
        return ActivityTimelineLayout.PreviousEventStart(events, at, PreviousSlackSeconds / durationSeconds) is { } start
            ? Math.Max((start * durationSeconds) - LeadInSeconds, 0)
            : null;
    }
}