namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// When the Timeline page reloads by itself: while it shows a span up to now, on an event that
/// changes it (activity started or ended, a recording started or stopped), at most every
/// <see cref="MinimumInterval"/> so a busy site does not reload it all the time.
/// </summary>
public static class TimelineRefresh
{
    /// <summary>
    /// The shortest time between two reloads.
    /// </summary>
    public static readonly TimeSpan MinimumInterval = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Determines whether an event changes what the page shows.
    /// </summary>
    /// <param name="surveillanceEvent">The event from <c>GET /events</c>.</param>
    /// <param name="showsNow">Whether the page shows a span up to now (not a past day).</param>
    public static bool ShouldReload(
        SurveillanceEvent surveillanceEvent,
        bool showsNow)
    {
        ArgumentNullException.ThrowIfNull(surveillanceEvent);

        return showsNow && (surveillanceEvent.Activity is not null || surveillanceEvent.Recording is not null);
    }

    /// <summary>
    /// Gets how long to wait before the next reload.
    /// </summary>
    /// <param name="lastLoad">When the page last loaded.</param>
    /// <param name="now">Now.</param>
    /// <returns>Zero when it may reload at once.</returns>
    public static TimeSpan Wait(
        DateTimeOffset lastLoad,
        DateTimeOffset now)
    {
        var due = lastLoad + MinimumInterval - now;
        return due > TimeSpan.Zero ? due : TimeSpan.Zero;
    }
}