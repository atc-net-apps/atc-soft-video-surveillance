namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// What *play all from here* plays on the Timeline page: from which moment, and a tile for each
/// camera with a recording after it. A browser opens only six connections to a server, one of them
/// the event stream, so the page plays at most <see cref="MaxTiles"/> cameras at once.
/// </summary>
public static class PlayAllPlan
{
    /// <summary>The most tiles the page plays at once.</summary>
    public const int MaxTiles = 4;

    /// <summary>
    /// Gets the moment to play from: the place last clicked or played, else the first recording in
    /// the span.
    /// </summary>
    /// <param name="lanes">The lanes shown.</param>
    /// <param name="cursor">The place last clicked or played, if any.</param>
    /// <param name="span">The span shown.</param>
    public static DateTimeOffset Moment(
        IEnumerable<TimelineLane> lanes,
        DateTimeOffset? cursor,
        TimelineSpan span)
    {
        ArgumentNullException.ThrowIfNull(lanes);
        ArgumentNullException.ThrowIfNull(span);

        return cursor ?? lanes
            .SelectMany(l => l.Coverage)
            .Select(c => c.Start < span.From ? span.From : c.Start)
            .DefaultIfEmpty(span.From)
            .Min();
    }

    /// <summary>
    /// Gets the tiles: every lane with a recording ending after the moment, its recordings in time
    /// order, the first <see cref="MaxTiles"/> of them.
    /// </summary>
    /// <param name="lanes">The lanes shown.</param>
    /// <param name="moment">The moment every tile plays from.</param>
    /// <param name="fileUrl">Turns a recording into the address the video element opens.</param>
    public static IReadOnlyList<PlayAllTileLogic> Tiles(
        IEnumerable<TimelineLane> lanes,
        DateTimeOffset moment,
        Func<string, string> fileUrl)
    {
        ArgumentNullException.ThrowIfNull(lanes);
        ArgumentNullException.ThrowIfNull(fileUrl);

        return [.. Candidates(lanes, moment, fileUrl).Take(MaxTiles)];
    }

    /// <summary>
    /// Gets the text telling that cameras were left out, or empty when every camera got a tile.
    /// </summary>
    /// <param name="lanes">The lanes shown.</param>
    /// <param name="moment">The moment every tile plays from.</param>
    public static string LimitText(
        IEnumerable<TimelineLane> lanes,
        DateTimeOffset moment)
        => Candidates(lanes, moment, path => path).Count() > MaxTiles
            ? string.Format(CultureInfo.CurrentCulture, Translations.PlayAllShowsFirst1, MaxTiles)
            : string.Empty;

    /// <summary>
    /// Gets the style of the grid of tiles: as square as the count allows.
    /// </summary>
    /// <param name="count">How many tiles.</param>
    public static string GridStyle(int count)
        => $"display: grid; grid-template-columns: repeat({Columns(count)}, 1fr); gap: 4px;";

    /// <summary>
    /// Gets the style of one tile: 16:9, and no taller than its share of most of the window, so
    /// every tile is seen without scrolling.
    /// </summary>
    /// <param name="count">How many tiles.</param>
    public static string TileStyle(int count)
    {
        var rows = (int)Math.Ceiling(Math.Max(count, 1) / (double)Columns(count));
        return $"position: relative; background: #111; aspect-ratio: 16 / 9; max-height: calc(70vh / {rows}); width: 100%;";
    }

    private static int Columns(int count)
        => (int)Math.Ceiling(Math.Sqrt(Math.Max(count, 1)));

    private static IEnumerable<PlayAllTileLogic> Candidates(
        IEnumerable<TimelineLane> lanes,
        DateTimeOffset moment,
        Func<string, string> fileUrl)
        => lanes
            .Select(l => (Lane: l, Segments: l.Coverage
                .Where(c => c.End > moment)
                .OrderBy(c => c.Start)
                .Select(c => new PlayAllSegment(fileUrl(c.FilePath), c.Start, c.RecordingStart))
                .ToList()))
            .Where(l => l.Segments.Count > 0)
            .Select(l => new PlayAllTileLogic(l.Lane.CameraName, l.Segments));
}