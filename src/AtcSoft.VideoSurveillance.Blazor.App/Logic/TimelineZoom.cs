namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// Zooming and moving the Timeline page: in and out around a moment (the place last played,
/// which stays where it is on the screen), earlier and later by half a span; never shorter than
/// <see cref="Shortest"/>, never longer than <see cref="Longest"/> (what the server gives), never
/// past now. A copy of Core's <c>TimelineZoom</c> on <see cref="TimelineSpan"/>, kept equal by a test.
/// </summary>
public static class TimelineZoom
{
    /// <summary>
    /// The shortest span: ten minutes.
    /// </summary>
    public static readonly TimeSpan Shortest = TimeSpan.FromMinutes(10);

    /// <summary>
    /// The longest span: seven days, the most the server gives.
    /// </summary>
    public static readonly TimeSpan Longest = TimeSpan.FromDays(7);

    /// <summary>
    /// Zooms in to half the span around a moment.
    /// </summary>
    /// <param name="span">The span.</param>
    /// <param name="focus">The moment that stays where it is; the middle when it is outside the span.</param>
    /// <param name="now">Now: the span never goes past it.</param>
    public static TimelineSpan In(
        TimelineSpan span,
        DateTimeOffset focus,
        DateTimeOffset now)
        => Scale(span, focus, now, 0.5);

    /// <summary>
    /// Zooms out to twice the span around a moment.
    /// </summary>
    /// <param name="span">The span.</param>
    /// <param name="focus">The moment that stays where it is; the middle when it is outside the span.</param>
    /// <param name="now">Now: the span never goes past it.</param>
    public static TimelineSpan Out(
        TimelineSpan span,
        DateTimeOffset focus,
        DateTimeOffset now)
        => Scale(span, focus, now, 2);

    /// <summary>
    /// Moves the span half its length earlier or later; later stops at now.
    /// </summary>
    /// <param name="span">The span.</param>
    /// <param name="later">Whether to move later; earlier when <see langword="false"/>.</param>
    /// <param name="now">Now: the span never goes past it.</param>
    public static TimelineSpan Move(
        TimelineSpan span,
        bool later,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(span);

        var half = (span.To - span.From) / 2;
        return KeepBeforeNow(later ? span.From + half : span.From - half, later ? span.To + half : span.To - half, now);
    }

    private static TimelineSpan Scale(
        TimelineSpan span,
        DateTimeOffset focus,
        DateTimeOffset now,
        double factor)
    {
        ArgumentNullException.ThrowIfNull(span);

        var length = span.To - span.From;
        if (focus < span.From || focus > span.To)
        {
            focus = span.From + (length / 2);
        }

        var scaled = length * factor;
        if (scaled < Shortest)
        {
            scaled = Shortest;
        }
        else if (scaled > Longest)
        {
            scaled = Longest;
        }

        // The focus keeps its place on the screen.
        var share = length > TimeSpan.Zero ? (focus - span.From) / length : 0.5;
        var start = focus - (scaled * share);
        return KeepBeforeNow(start, start + scaled, now);
    }

    private static TimelineSpan KeepBeforeNow(
        DateTimeOffset from,
        DateTimeOffset to,
        DateTimeOffset now)
        => to > now
            ? new TimelineSpan(from - (to - now), now)
            : new TimelineSpan(from, to);
}