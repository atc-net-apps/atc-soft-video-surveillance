namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// The span the Timeline page shows: the last 24 hours, or one day of the viewer's calendar.
/// </summary>
/// <param name="From">The start, in UTC.</param>
/// <param name="To">The end, in UTC.</param>
public sealed record TimelineSpan(
    DateTimeOffset From,
    DateTimeOffset To)
{
    /// <summary>
    /// Gets the last 24 hours up to now.
    /// </summary>
    /// <param name="now">Now.</param>
    public static TimelineSpan Last24Hours(DateTimeOffset now)
        => new(now.AddHours(-24), now);

    /// <summary>
    /// Gets one day of the viewer's calendar, midnight to midnight in their time zone (23 or 25
    /// hours on the days the clocks change).
    /// </summary>
    /// <param name="day">The day.</param>
    /// <param name="zone">The viewer's time zone.</param>
    public static TimelineSpan Day(
        DateOnly day,
        TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(zone);

        return new TimelineSpan(Midnight(day, zone), Midnight(day.AddDays(1), zone));
    }

    /// <summary>
    /// Gets the moment at a fraction of the span.
    /// </summary>
    /// <param name="fraction">0 is the start, 1 the end.</param>
    public DateTimeOffset At(double fraction)
        => From + ((To - From) * Math.Clamp(fraction, 0, 1));

    /// <summary>
    /// Gets where a moment is in the span, as a fraction.
    /// </summary>
    /// <param name="moment">The moment.</param>
    /// <returns>0 to 1, or <see langword="null"/> when the moment is outside the span.</returns>
    public double? FractionOf(DateTimeOffset moment)
        => moment < From || moment > To ? null : (moment - From) / (To - From);

    private static DateTimeOffset Midnight(
        DateOnly day,
        TimeZoneInfo zone)
    {
        var local = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, zone.GetUtcOffset(local)).ToUniversalTime();
    }
}