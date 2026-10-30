namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// Finding a recording's preview picture for the place under the mouse, in the JPEG of tiles the
/// server makes: on an event, the picture of its busiest second, elsewhere the nearest one of the
/// grid. The look-ups are a copy of Core's <c>RecordingPreviewLayout</c>, kept equal by a test;
/// the choice is the desktop timeline's.
/// </summary>
public static class RecordingPreviewLayout
{
    /// <summary>
    /// Gets the grid's tile of a moment: the nearest one.
    /// </summary>
    /// <param name="previews">The layout.</param>
    /// <param name="seconds">How far into the recording.</param>
    public static int GridTile(
        RecordingPreviewSheet previews,
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
        RecordingPreviewSheet previews,
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
        RecordingPreviewSheet previews,
        int tile)
    {
        ArgumentNullException.ThrowIfNull(previews);

        var columns = Math.Max(previews.Columns, 1);
        return ((tile % columns) * previews.TileWidth, (tile / columns) * previews.TileHeight, previews.TileWidth, previews.TileHeight);
    }

    /// <summary>
    /// Picks the picture for a place on the timeline: the busiest second of an event there, else
    /// the nearest tile of the grid.
    /// </summary>
    /// <param name="previews">The layout; one without previews has no picture.</param>
    /// <param name="events">The events of the recording, as fractions of it.</param>
    /// <param name="from">Where the place starts, 0 to 1.</param>
    /// <param name="to">Where it ends, 0 to 1.</param>
    /// <param name="durationSeconds">How long the recording is, in seconds.</param>
    /// <returns>The tile and the moment it shows, as a fraction of the recording; <see langword="null"/> without a picture.</returns>
    public static (int Tile, double Position)? Pick(
        RecordingPreviewSheet? previews,
        IReadOnlyList<ActivityTimelineEvent> events,
        double from,
        double to,
        double durationSeconds)
    {
        ArgumentNullException.ThrowIfNull(events);

        if (previews is not { HasPreviews: true, GridCount: > 0 } || durationSeconds <= 0)
        {
            return null;
        }

        if (events.FirstOrDefault(e => e.End > from && e.Start < to) is { } activityEvent &&
            EventTile(previews, activityEvent.Start * durationSeconds, activityEvent.End * durationSeconds) is { } eventTile)
        {
            return (eventTile, previews.Events.First(e => e.Tile == eventTile).Seconds / durationSeconds);
        }

        var gridTile = GridTile(previews, (from + to) / 2 * durationSeconds);
        return (gridTile, Math.Min(gridTile * previews.IntervalSeconds / durationSeconds, 1));
    }

    /// <summary>
    /// Gets the CSS that shows one tile of the JPEG as a box of the tile's size.
    /// </summary>
    /// <param name="previews">The layout.</param>
    /// <param name="tile">The tile's number.</param>
    /// <param name="imageUrl">Where the JPEG is fetched, with the media token.</param>
    [SuppressMessage(
        "Design",
        "CA1054:URI parameters should not be strings",
        Justification = "As RecordingDisplay builds it and CSS takes it.")]
    public static string TileStyle(
        RecordingPreviewSheet previews,
        int tile,
        string imageUrl)
    {
        ArgumentNullException.ThrowIfNull(imageUrl);

        var (x, y, width, height) = TileRect(previews, tile);
        var url = imageUrl.Replace("'", "%27", StringComparison.Ordinal);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"width: {width}px; height: {height}px; background-image: url('{url}'); background-position: -{x}px -{y}px; background-repeat: no-repeat;");
    }
}