namespace AtcSoft.VideoSurveillance.Helpers.Activity;

/// <summary>
/// The marks under a timeline of every camera, on whole hours of the viewer's clock: every hour
/// for a few hours, every three hours for a day, every midnight for longer spans. The web client
/// has a copy under <c>Logic/</c>, kept equal by a test.
/// </summary>
public static class TimelineMarks
{
    /// <summary>
    /// Gets the marks of a span.
    /// </summary>
    /// <param name="from">The start of the span.</param>
    /// <param name="to">The end of the span, after <paramref name="from"/>.</param>
    /// <param name="zone">The viewer's time zone.</param>
    /// <param name="culture">How times and days are written.</param>
    public static IReadOnlyList<TimelineMark> For(
        DateTimeOffset from,
        DateTimeOffset to,
        TimeZoneInfo zone,
        CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(zone);
        ArgumentNullException.ThrowIfNull(culture);

        var marks = new List<TimelineMark>();
        if (to <= from)
        {
            return marks;
        }

        var length = to - from;
        var everyHours = length <= TimeSpan.FromHours(6) ? 1 : length <= TimeSpan.FromHours(36) ? 3 : 24;
        var days = everyHours == 24;

        var local = TimeZoneInfo.ConvertTime(from, zone);
        var mark = new DateTime(local.Year, local.Month, local.Day, local.Hour, 0, 0, DateTimeKind.Unspecified);
        for (var guard = 0; guard < 400; guard++, mark = mark.AddHours(1))
        {
            var moment = new DateTimeOffset(mark, zone.GetUtcOffset(mark));
            if (moment > to)
            {
                break;
            }

            if (moment < from || mark.Hour % everyHours != 0)
            {
                continue;
            }

            var label = days
                ? mark.ToString("ddd d", culture)
                : mark.ToString("t", culture);
            marks.Add(new TimelineMark((moment - from) / length, label));
        }

        return marks;
    }
}