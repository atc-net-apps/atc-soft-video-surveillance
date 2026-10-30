namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// The marks under the Timeline page's lanes, on whole hours of the viewer's clock: every hour
/// for a few hours, every three hours for a day, every midnight for longer spans. A copy of
/// Core's <c>TimelineMarks</c>, kept equal by a test.
/// </summary>
public static class TimelineTicks
{
    /// <summary>
    /// Gets the marks of a span.
    /// </summary>
    /// <param name="span">The span.</param>
    /// <param name="zone">The viewer's time zone.</param>
    /// <param name="culture">How times and days are written.</param>
    public static IReadOnlyList<TimelineTick> For(
        TimelineSpan span,
        TimeZoneInfo zone,
        CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(span);
        ArgumentNullException.ThrowIfNull(zone);
        ArgumentNullException.ThrowIfNull(culture);

        var ticks = new List<TimelineTick>();
        if (span.To <= span.From)
        {
            return ticks;
        }

        var length = span.To - span.From;
        var everyHours = length <= TimeSpan.FromHours(6) ? 1 : length <= TimeSpan.FromHours(36) ? 3 : 24;
        var days = everyHours == 24;

        var local = TimeZoneInfo.ConvertTime(span.From, zone);
        var mark = new DateTime(local.Year, local.Month, local.Day, local.Hour, 0, 0, DateTimeKind.Unspecified);
        for (var guard = 0; guard < 400; guard++, mark = mark.AddHours(1))
        {
            var moment = new DateTimeOffset(mark, zone.GetUtcOffset(mark));
            if (moment > span.To)
            {
                break;
            }

            if (moment < span.From || mark.Hour % everyHours != 0)
            {
                continue;
            }

            var label = days
                ? mark.ToString("ddd d", culture)
                : mark.ToString("t", culture);
            ticks.Add(new TimelineTick((moment - span.From) / length, label));
        }

        return ticks;
    }
}