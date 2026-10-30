namespace AtcSoft.VideoSurveillance.Helpers.Activity;

/// <summary>
/// Zooming and moving a timeline of every camera: in and out around a moment (the place last
/// played, which stays where it is on the screen), earlier and later by half a span. A span is
/// never shorter than <see cref="Shortest"/>, never longer than <see cref="Longest"/> (what the
/// server gives), and never goes past now. The web client has a copy under <c>Logic/</c>, kept
/// equal by a test.
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
    /// <param name="from">The start of the span.</param>
    /// <param name="to">The end of the span.</param>
    /// <param name="focus">The moment that stays where it is; the middle when it is outside the span.</param>
    /// <param name="now">Now: the span never goes past it.</param>
    public static (DateTimeOffset From, DateTimeOffset To) In(
        DateTimeOffset from,
        DateTimeOffset to,
        DateTimeOffset focus,
        DateTimeOffset now)
        => Scale(from, to, focus, now, 0.5);

    /// <summary>
    /// Zooms out to twice the span around a moment.
    /// </summary>
    /// <param name="from">The start of the span.</param>
    /// <param name="to">The end of the span.</param>
    /// <param name="focus">The moment that stays where it is; the middle when it is outside the span.</param>
    /// <param name="now">Now: the span never goes past it.</param>
    public static (DateTimeOffset From, DateTimeOffset To) Out(
        DateTimeOffset from,
        DateTimeOffset to,
        DateTimeOffset focus,
        DateTimeOffset now)
        => Scale(from, to, focus, now, 2);

    /// <summary>
    /// Moves the span half its length earlier or later; later stops at now.
    /// </summary>
    /// <param name="from">The start of the span.</param>
    /// <param name="to">The end of the span.</param>
    /// <param name="later">Whether to move later; earlier when <see langword="false"/>.</param>
    /// <param name="now">Now: the span never goes past it.</param>
    public static (DateTimeOffset From, DateTimeOffset To) Move(
        DateTimeOffset from,
        DateTimeOffset to,
        bool later,
        DateTimeOffset now)
    {
        var half = (to - from) / 2;
        return KeepBeforeNow(later ? from + half : from - half, later ? to + half : to - half, now);
    }

    private static (DateTimeOffset From, DateTimeOffset To) Scale(
        DateTimeOffset from,
        DateTimeOffset to,
        DateTimeOffset focus,
        DateTimeOffset now,
        double factor)
    {
        var length = to - from;
        if (focus < from || focus > to)
        {
            focus = from + (length / 2);
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
        var share = length > TimeSpan.Zero ? (focus - from) / length : 0.5;
        var start = focus - (scaled * share);
        return KeepBeforeNow(start, start + scaled, now);
    }

    private static (DateTimeOffset From, DateTimeOffset To) KeepBeforeNow(
        DateTimeOffset from,
        DateTimeOffset to,
        DateTimeOffset now)
        => to > now
            ? (from - (to - now), now)
            : (from, to);
}