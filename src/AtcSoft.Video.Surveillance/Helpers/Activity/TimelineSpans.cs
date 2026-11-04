namespace AtcSoft.Video.Surveillance.Helpers.Activity;

/// <summary>
/// The spans a timeline offers (the last hour up to the last 24 hours, or a chosen day), and the
/// part of a span a range slider zooms into.
/// </summary>
public static class TimelineSpans
{
    /// <summary>
    /// Gets the "last hours" a timeline offers, shortest first.
    /// </summary>
    public static IReadOnlyList<int> LastHoursChoices { get; } = [1, 3, 6, 12, 24];

    /// <summary>
    /// Gets the last hours up to now.
    /// </summary>
    /// <param name="hours">How many hours back.</param>
    /// <param name="now">Now.</param>
    /// <returns>The span.</returns>
    public static (DateTimeOffset From, DateTimeOffset To) LastHours(
        int hours,
        DateTimeOffset now)
        => (now.AddHours(-Math.Max(1, hours)), now);

    /// <summary>
    /// Gets the part of a span between two places on a slider over it, in minutes from its start:
    /// kept inside the span and at least <see cref="TimelineZoom.Shortest"/> long (or the whole span
    /// when it is shorter).
    /// </summary>
    /// <param name="from">The span's start.</param>
    /// <param name="to">The span's end.</param>
    /// <param name="startMinutes">Where the part starts, in minutes from the span's start.</param>
    /// <param name="endMinutes">Where the part ends.</param>
    /// <returns>The part.</returns>
    public static (DateTimeOffset From, DateTimeOffset To) Within(
        DateTimeOffset from,
        DateTimeOffset to,
        double startMinutes,
        double endMinutes)
    {
        var length = to - from;
        if (length <= TimelineZoom.Shortest)
        {
            return (from, to);
        }

        var total = length.TotalMinutes;
        var start = Math.Clamp(Math.Min(startMinutes, endMinutes), 0, total);
        var end = Math.Clamp(Math.Max(startMinutes, endMinutes), 0, total);
        var shortest = TimelineZoom.Shortest.TotalMinutes;
        if (end - start < shortest)
        {
            // Grown around its middle, then pushed back inside the span.
            var middle = (start + end) / 2;
            start = Math.Clamp(middle - (shortest / 2), 0, total - shortest);
            end = start + shortest;
        }

        return (from + TimeSpan.FromMinutes(start), from + TimeSpan.FromMinutes(end));
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
}