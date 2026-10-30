namespace AtcSoft.VideoSurveillance.Helpers.Activity;

/// <summary>
/// The span a timeline of every camera shows: one day of the viewer's calendar, midnight to
/// midnight in the viewer's zone (23 or 25 hours when the clocks change), or the last 24 hours.
/// </summary>
public static class TimelineDays
{
    /// <summary>
    /// Gets the span shown.
    /// </summary>
    /// <param name="day">The day, or <see langword="null"/> for the last 24 hours.</param>
    /// <param name="now">Now.</param>
    /// <param name="zone">The viewer's time zone.</param>
    public static (DateTimeOffset From, DateTimeOffset To) Span(
        DateTime? day,
        DateTimeOffset now,
        TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        if (day is not { } chosen)
        {
            return (now.AddHours(-24), now);
        }

        var midnight = chosen.Date;
        var next = midnight.AddDays(1);
        return (new DateTimeOffset(midnight, zone.GetUtcOffset(midnight)), new DateTimeOffset(next, zone.GetUtcOffset(next)));
    }
}