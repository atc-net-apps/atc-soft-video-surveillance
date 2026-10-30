namespace AtcSoft.VideoSurveillance.Helpers.Activity;

/// <summary>
/// The layout of a recording's preview pictures: how many tiles and how big, which moment each
/// shows, and where a tile is in the JPEG. A grid of tiles evenly over the recording (at most
/// <see cref="MostGridTiles"/>, at least a second apart), then a tile for each event, of the second
/// most moved in it, so a short event between two grid tiles still has its picture. The web client
/// has a copy of the look-ups under <c>Logic/</c>, kept equal by a test.
/// </summary>
public static class RecordingPreviewLayout
{
    /// <summary>
    /// The longest side of a tile, in pixels.
    /// </summary>
    public const int LongestSide = 160;

    /// <summary>
    /// The most tiles the grid has.
    /// </summary>
    public const int MostGridTiles = 600;

    /// <summary>
    /// The most events that get a tile of their own; the first ones.
    /// </summary>
    public const int MostEventTiles = 200;

    /// <summary>
    /// How many tiles a row of the JPEG has.
    /// </summary>
    public const int Columns = 20;

    /// <summary>
    /// The shortest time between two of the grid's tiles, in seconds.
    /// </summary>
    public const double ShortestInterval = 1;

    /// <summary>
    /// Gets the size of a tile for a picture of the displayed size: its longest side
    /// <see cref="LongestSide"/>, the other one even, keeping the picture's shape.
    /// </summary>
    /// <param name="displayWidth">The picture's width, as displayed.</param>
    /// <param name="displayHeight">The picture's height, as displayed.</param>
    public static (int Width, int Height) TileSize(
        int displayWidth,
        int displayHeight)
    {
        if (displayWidth <= 0 || displayHeight <= 0)
        {
            return (LongestSide, LongestSide * 9 / 16);
        }

        return displayWidth >= displayHeight
            ? (LongestSide, Even(LongestSide * (double)displayHeight / displayWidth))
            : (Even(LongestSide * (double)displayWidth / displayHeight), LongestSide);
    }

    /// <summary>
    /// Plans the previews of a recording: the layout, and the moments to take a picture of for
    /// each tile, in the order they come in the recording.
    /// </summary>
    /// <param name="durationSeconds">How long the recording is, in seconds.</param>
    /// <param name="displayWidth">The picture's width, as displayed.</param>
    /// <param name="displayHeight">The picture's height, as displayed.</param>
    /// <param name="eventSeconds">The moment most moved in each event (<see cref="PeakSeconds"/>), from the start.</param>
    /// <returns>The layout, and each tile with its moment, by moment.</returns>
    public static (RecordingPreviews Previews, IReadOnlyList<(double Seconds, int Tile)> Moments) Plan(
        double durationSeconds,
        int displayWidth,
        int displayHeight,
        IReadOnlyList<double> eventSeconds)
    {
        ArgumentNullException.ThrowIfNull(eventSeconds);

        var duration = Math.Max(durationSeconds, 0);
        var interval = Math.Max(ShortestInterval, duration / (MostGridTiles - 1));
        var gridCount = Math.Min(MostGridTiles, (int)Math.Floor((duration / interval) + 1e-9) + 1);
        var (tileWidth, tileHeight) = TileSize(displayWidth, displayHeight);

        var events = eventSeconds
            .Where(s => s >= 0 && s <= duration)
            .Take(MostEventTiles)
            .Select((seconds, i) => new RecordingPreviewEvent(seconds, gridCount + i))
            .ToList();

        var moments = Enumerable.Range(0, gridCount)
            .Select(i => (Seconds: i * interval, Tile: i))
            .Concat(events.Select(e => (e.Seconds, e.Tile)))
            .OrderBy(m => m.Seconds)
            .ThenBy(m => m.Tile)
            .ToList();

        return (new RecordingPreviews(interval, tileWidth, tileHeight, Columns, gridCount, events), moments);
    }

    /// <summary>
    /// Gets the second most moved in each event of a recording: the most moving at once, then the
    /// most changed, the earlier on a tie. Its middle, in seconds into the recording.
    /// </summary>
    /// <param name="buckets">The recording's activity, a stretch a second (<see cref="ActivityDownsampler.ByMax"/>).</param>
    /// <param name="durationSeconds">How long the recording is, in seconds.</param>
    public static IReadOnlyList<double> PeakSeconds(
        IReadOnlyList<ActivityBucket> buckets,
        double durationSeconds)
    {
        ArgumentNullException.ThrowIfNull(buckets);

        var count = buckets.Count;
        if (count == 0 || durationSeconds <= 0)
        {
            return [];
        }

        var stretch = durationSeconds / count;
        var peaks = new List<double>();
        foreach (var activityEvent in ActivityTimelineLayout.FindEvents(buckets, durationSeconds))
        {
            var from = (int)Math.Round(activityEvent.Start * count);
            var to = Math.Min((int)Math.Round(activityEvent.End * count), count);
            var peak = from;
            for (var i = from + 1; i < to; i++)
            {
                var (best, here) = (buckets[peak], buckets[i]);
                if (here.Tracks > best.Tracks || (here.Tracks == best.Tracks && here.Level > best.Level))
                {
                    peak = i;
                }
            }

            peaks.Add((peak + 0.5) * stretch);
        }

        return peaks;
    }

    /// <summary>
    /// Gets the grid's tile of a moment: the nearest one.
    /// </summary>
    /// <param name="previews">The layout.</param>
    /// <param name="seconds">How far into the recording.</param>
    public static int GridTile(
        RecordingPreviews previews,
        double seconds)
    {
        ArgumentNullException.ThrowIfNull(previews);

        return previews.GridCount <= 0 || previews.IntervalSeconds <= 0
            ? 0
            : Math.Clamp((int)Math.Round(seconds / previews.IntervalSeconds), 0, previews.GridCount - 1);
    }

    /// <summary>
    /// Gets the tile of an event between two moments: the first event tile whose moment lies there.
    /// </summary>
    /// <param name="previews">The layout.</param>
    /// <param name="fromSeconds">The event's start, in seconds into the recording.</param>
    /// <param name="toSeconds">The event's end.</param>
    /// <returns>The tile, or <see langword="null"/> when the event has none.</returns>
    public static int? EventTile(
        RecordingPreviews previews,
        double fromSeconds,
        double toSeconds)
    {
        ArgumentNullException.ThrowIfNull(previews);

        return previews.Events.FirstOrDefault(e => e.Seconds >= fromSeconds && e.Seconds <= toSeconds)?.Tile;
    }

    /// <summary>
    /// Gets where a tile is in the JPEG, in pixels.
    /// </summary>
    /// <param name="previews">The layout.</param>
    /// <param name="tile">The tile's number.</param>
    public static (int X, int Y, int Width, int Height) TileRect(
        RecordingPreviews previews,
        int tile)
    {
        ArgumentNullException.ThrowIfNull(previews);

        var columns = Math.Max(previews.Columns, 1);
        return ((tile % columns) * previews.TileWidth, (tile / columns) * previews.TileHeight, previews.TileWidth, previews.TileHeight);
    }

    /// <summary>
    /// Gets the size of the JPEG that holds every tile.
    /// </summary>
    /// <param name="previews">The layout.</param>
    public static (int Width, int Height) SheetSize(RecordingPreviews previews)
    {
        ArgumentNullException.ThrowIfNull(previews);

        var tiles = Math.Max(previews.GridCount + previews.Events.Count, 1);
        var columns = Math.Clamp(tiles, 1, Math.Max(previews.Columns, 1));
        var rows = (tiles + columns - 1) / columns;
        return (columns * previews.TileWidth, rows * previews.TileHeight);
    }

    private static int Even(double value)
        => Math.Max(2, (int)Math.Round(value / 2) * 2);
}