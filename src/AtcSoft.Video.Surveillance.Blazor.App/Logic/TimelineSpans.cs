namespace AtcSoft.Video.Surveillance.Blazor.App.Logic;

/// <summary>
/// The spans the timeline offers (the last hour up to the last 24 hours, or a chosen day), and the
/// part of a span its range slider zooms into. The web client's copy of Core's
/// <c>TimelineSpans</c>; a test keeps the two equal.
/// </summary>
public static class TimelineSpans
{
    /// <summary>
    /// The shortest part a range slider shows.
    /// </summary>
    public static readonly TimeSpan Shortest = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Gets the "last hours" the timeline offers, shortest first.
    /// </summary>
    public static IReadOnlyList<int> LastHoursChoices { get; } = [1, 3, 6, 12, 24];

    /// <summary>
    /// Gets the last hours up to now.
    /// </summary>
    /// <param name="hours">How many hours back.</param>
    /// <param name="now">Now.</param>
    /// <returns>The span.</returns>
    public static TimelineSpan LastHours(
        int hours,
        DateTimeOffset now)
        => new(now.AddHours(-Math.Max(1, hours)), now);

    /// <summary>
    /// Gets the whole span chosen: the last hours up to now, or a day of the viewer's calendar
    /// (<paramref name="hours"/> 0; today when no day is chosen).
    /// </summary>
    /// <param name="hours">How many hours back; 0 for a day.</param>
    /// <param name="day">The day chosen, or <see langword="null"/>.</param>
    /// <param name="now">Now.</param>
    /// <param name="zone">The viewer's time zone.</param>
    /// <returns>The span.</returns>
    public static TimelineSpan Whole(
        int hours,
        DateOnly? day,
        DateTimeOffset now,
        TimeZoneInfo zone)
    {
        if (hours > 0)
        {
            return LastHours(hours, now);
        }

        return TimelineSpan.Day(day ?? DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, zone).DateTime), zone);
    }

    /// <summary>
    /// Gets the part of a span between two places on a slider over it, in minutes from its start:
    /// kept inside the span and at least <see cref="Shortest"/> long (or the whole span when it is
    /// shorter).
    /// </summary>
    /// <param name="span">The span.</param>
    /// <param name="startMinutes">Where the part starts, in minutes from the span's start.</param>
    /// <param name="endMinutes">Where the part ends.</param>
    /// <returns>The part.</returns>
    public static TimelineSpan Within(
        TimelineSpan span,
        double startMinutes,
        double endMinutes)
    {
        ArgumentNullException.ThrowIfNull(span);

        var length = span.To - span.From;
        if (length <= Shortest)
        {
            return span;
        }

        var total = length.TotalMinutes;
        var start = Math.Clamp(Math.Min(startMinutes, endMinutes), 0, total);
        var end = Math.Clamp(Math.Max(startMinutes, endMinutes), 0, total);
        var shortest = Shortest.TotalMinutes;
        if (end - start < shortest)
        {
            // Grown around its middle, then pushed back inside the span.
            var middle = (start + end) / 2;
            start = Math.Clamp(middle - (shortest / 2), 0, total - shortest);
            end = start + shortest;
        }

        return new TimelineSpan(span.From + TimeSpan.FromMinutes(start), span.From + TimeSpan.FromMinutes(end));
    }

    /// <summary>
    /// Gets whether a part of a span is all of it, so a span up to now may grow as time goes on.
    /// </summary>
    /// <param name="startMinutes">Where the part starts, in minutes from the span's start.</param>
    /// <param name="endMinutes">Where the part ends.</param>
    /// <param name="totalMinutes">The span's length in minutes.</param>
    public static bool IsWhole(
        double startMinutes,
        double endMinutes,
        double totalMinutes)
        => startMinutes <= 0.5 && endMinutes >= totalMinutes - 0.5;

    /// <summary>
    /// Gets where a range slider's highlighted part lies, as CSS percentages of its width.
    /// </summary>
    /// <param name="start">The lower thumb's value.</param>
    /// <param name="end">The upper thumb's value.</param>
    /// <param name="maximum">The slider's maximum (its minimum is 0).</param>
    /// <returns>The left edge and the width.</returns>
    public static (string Left, string Width) Fill(
        double start,
        double end,
        double maximum)
    {
        if (maximum <= 0)
        {
            return ("0%", "0%");
        }

        var low = Math.Clamp(Math.Min(start, end) / maximum, 0, 1) * 100;
        var high = Math.Clamp(Math.Max(start, end) / maximum, 0, 1) * 100;
        return (
            low.ToString("0.##", CultureInfo.InvariantCulture) + "%",
            (high - low).ToString("0.##", CultureInfo.InvariantCulture) + "%");
    }

    /// <summary>
    /// Gets a span's label in the selector: the last hour, the last hours, or a chosen day (0).
    /// </summary>
    /// <param name="hours">How many hours back; 0 for a chosen day.</param>
    public static string Label(int hours)
        => hours switch
        {
            0 => Translations.ChooseADay,
            1 => Translations.LastHour,
            _ => string.Format(CultureInfo.CurrentCulture, Translations.LastHours1, hours),
        };

    /// <summary>
    /// Gets the text saying which part is shown.
    /// </summary>
    /// <param name="span">The part shown.</param>
    /// <param name="zone">The viewer's time zone.</param>
    /// <param name="culture">The viewer's culture.</param>
    public static string ShownText(
        TimelineSpan span,
        TimeZoneInfo zone,
        CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(span);
        ArgumentNullException.ThrowIfNull(zone);

        var from = TimeZoneInfo.ConvertTime(span.From, zone);
        var to = TimeZoneInfo.ConvertTime(span.To, zone);
        return string.Format(
            culture,
            Translations.Shown2,
            from.ToString("g", culture),
            to.ToString(from.Date == to.Date ? "t" : "g", culture));
    }
}