namespace AtcSoft.Video.Surveillance.Helpers.Activity;

/// <summary>
/// How often a camera dropped out over a span and for how long, as a timeline lane tells it beside
/// the camera's name: the drops are the times it went from online (or unknown) to offline in the
/// span, the length is what its offline gaps cover. A camera that keeps failing to reconnect is one
/// drop, however many times it tried.
/// </summary>
public static class OfflineSummary
{
    /// <summary>
    /// Sums up a camera's drops over a span.
    /// </summary>
    /// <param name="history">The camera's history, with the app's starts and stops.</param>
    /// <param name="gaps">The camera's gaps over the span (<see cref="TimelineGaps.Find"/>).</param>
    /// <param name="from">The start of the span.</param>
    /// <param name="to">The end of the span.</param>
    /// <returns>How many drops and how long offline.</returns>
    public static (int Drops, TimeSpan Offline) Of(
        IReadOnlyList<AvailabilityEntry> history,
        IReadOnlyList<TimelineGap> gaps,
        DateTimeOffset from,
        DateTimeOffset to)
    {
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(gaps);

        var drops = 0;
        var offline = false;
        var appDown = false;
        foreach (var entry in history.OrderBy(e => e.Time))
        {
            if (entry.Time > to)
            {
                break;
            }

            switch (entry.Change)
            {
                // A camera let go of as the app closes is not a drop.
                case AvailabilityChange.Offline when !appDown:
                    if (!offline && entry.Time >= from)
                    {
                        drops++;
                    }

                    offline = true;
                    break;
                case AvailabilityChange.Online:
                case AvailabilityChange.Released:
                    offline = false;
                    break;
                case AvailabilityChange.ServerStarted:
                    appDown = false;
                    offline = false;
                    break;
                case AvailabilityChange.ServerStopped:
                    appDown = true;
                    offline = false;
                    break;
            }
        }

        var length = gaps
            .Where(g => g.Reason == GapReason.Offline)
            .Aggregate(TimeSpan.Zero, (sum, g) => sum + (g.End - g.Start));
        return (drops, length);
    }
}