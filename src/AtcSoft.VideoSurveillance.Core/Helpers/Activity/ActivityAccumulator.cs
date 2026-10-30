namespace AtcSoft.VideoSurveillance.Helpers.Activity;

/// <summary>
/// Turns the detector's result for every analysed picture into one entry per second: the highest
/// level, the most tracks at once and the ids seen. A second is complete when a picture of a
/// later second arrives, or when <see cref="Flush"/> is called at the end.
/// </summary>
public sealed class ActivityAccumulator
{
    private readonly SortedSet<int> trackIds = [];
    private readonly List<string> zones = [];
    private int? offset;
    private double level;
    private int tracks;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityAccumulator"/> class.
    /// </summary>
    /// <param name="start">Where the offsets count from.</param>
    public ActivityAccumulator(DateTimeOffset start)
    {
        Start = start;
    }

    /// <summary>
    /// Gets where the offsets count from.
    /// </summary>
    public DateTimeOffset Start { get; }

    /// <summary>
    /// Adds the result of one analysed picture.
    /// </summary>
    /// <param name="time">When the picture was taken.</param>
    /// <param name="result">What the detector found in it.</param>
    /// <returns>The second that this picture completed, or <see langword="null"/> while the second goes on.</returns>
    public ActivitySecond? Add(
        DateTimeOffset time,
        MotionDetectorResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        // A clock that steps back stays in the current second; offsets never go down.
        var second = Math.Max((int)Math.Floor((time - Start).TotalSeconds), 0);
        if (offset is { } current && second <= current)
        {
            Include(result);
            return null;
        }

        var completed = Flush();
        offset = second;
        Include(result);
        return completed;
    }

    /// <summary>
    /// Completes the second in progress.
    /// </summary>
    /// <returns>The second, or <see langword="null"/> when no picture has been added since the last one.</returns>
    public ActivitySecond? Flush()
    {
        if (offset is not { } current)
        {
            return null;
        }

        var completed = new ActivitySecond(current, Math.Round(level, 1), tracks, [.. trackIds]) { Zones = [.. zones] };
        offset = null;
        level = 0;
        tracks = 0;
        trackIds.Clear();
        zones.Clear();
        return completed;
    }

    private void Include(MotionDetectorResult result)
    {
        level = Math.Max(level, Math.Clamp(result.ChangePercent, 0, 100));
        tracks = Math.Max(tracks, result.Boxes.Count);
        foreach (var box in result.Boxes)
        {
            if (box.TrackId is { } id)
            {
                trackIds.Add(id);
            }

            if (box.Zone is { Length: > 0 } zone && !zones.Contains(zone, StringComparer.Ordinal))
            {
                zones.Add(zone);
            }
        }
    }
}