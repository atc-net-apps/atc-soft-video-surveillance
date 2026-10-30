namespace AtcSoft.VideoSurveillance.Helpers.Activity;

/// <summary>
/// Keeps the tiles of *play all from here* roughly in step. A shared clock starts at the chosen
/// moment and is the moment of the slowest tile playing: the slowest sets the pace. A tile
/// more than <see cref="HoldAhead"/> ahead is held until it is within <see cref="ResumeWithin"/>; a
/// tile is never moved forward. A tile between recordings starts its next one when the clock gets
/// there, and one that loads, or plays without moving, longer than <see cref="GiveUpAfter"/> is
/// given up, so it does not hold the others forever. With no tile playing, loading or held, the clock jumps to the next recording
/// of any tile: a stretch where no camera recorded anything is skipped.
/// </summary>
/// <remarks>
/// The web client has a copy in <c>Logic/</c>; a test runs the same ticks through both.
/// </remarks>
public sealed class PlayAllPacer
{
    /// <summary>How far ahead a tile may run before it is held.</summary>
    public static readonly TimeSpan HoldAhead = TimeSpan.FromSeconds(1);

    /// <summary>How close a held tile must be before it plays again.</summary>
    public static readonly TimeSpan ResumeWithin = TimeSpan.FromSeconds(0.25);

    /// <summary>How long a tile may load before it is given up.</summary>
    public static readonly TimeSpan GiveUpAfter = TimeSpan.FromSeconds(15);

    private readonly Dictionary<int, TimeSpan> loading = [];
    private readonly Dictionary<int, (DateTimeOffset At, TimeSpan Still)> playing = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="PlayAllPacer"/> class.
    /// </summary>
    /// <param name="moment">The moment every tile plays from.</param>
    public PlayAllPacer(DateTimeOffset moment)
        => Clock = moment;

    /// <summary>
    /// Gets the shared clock: the moment the tiles are at.
    /// </summary>
    public DateTimeOffset Clock { get; private set; }

    /// <summary>
    /// Gets or sets a value indicating whether everything is paused by the viewer: the clock stands
    /// still and no tile is asked to do anything.
    /// </summary>
    public bool IsPaused { get; set; }

    /// <summary>
    /// Moves the clock on and tells each tile what to do.
    /// </summary>
    /// <param name="elapsed">The real time since the last tick.</param>
    /// <param name="tiles">The tiles as their players report them, always in the same order.</param>
    /// <returns>One action per tile, in their order.</returns>
    public IReadOnlyList<PlayAllAction> Tick(
        TimeSpan elapsed,
        IReadOnlyList<PlayAllTile> tiles)
    {
        ArgumentNullException.ThrowIfNull(tiles);

        var actions = new PlayAllAction[tiles.Count];
        if (IsPaused)
        {
            return actions;
        }

        var playingAt = tiles
            .Where(t => t.State == PlayAllTileState.Playing && t.At is not null)
            .Select(t => t.At!.Value)
            .ToList();
        Clock = playingAt.Count > 0 ? playingAt.Min() : Clock + elapsed;
        var busy = tiles.Any(t => t.State is PlayAllTileState.Playing or PlayAllTileState.Loading or PlayAllTileState.Held);
        var nextStart = tiles
            .Where(t => t.State == PlayAllTileState.Waiting && t.NextStart is not null)
            .Select(t => t.NextStart!.Value)
            .DefaultIfEmpty(Clock)
            .Min();
        if (!busy && nextStart > Clock)
        {
            Clock = nextStart;
        }

        for (var i = 0; i < tiles.Count; i++)
        {
            var tile = tiles[i];
            actions[i] = tile.State switch
            {
                PlayAllTileState.Playing when StoodStillTooLong(i, tile.At, elapsed) => PlayAllAction.GiveUp,
                PlayAllTileState.Playing when tile.At - Clock > HoldAhead => PlayAllAction.Hold,
                PlayAllTileState.Held when tile.At is null || tile.At - Clock <= ResumeWithin => PlayAllAction.Resume,
                PlayAllTileState.Waiting when tile.NextStart is null || tile.NextStart <= Clock => PlayAllAction.Start,
                PlayAllTileState.Loading when LoadedTooLong(i, elapsed) => PlayAllAction.GiveUp,
                _ => PlayAllAction.None,
            };

            if (tile.State != PlayAllTileState.Loading)
            {
                loading.Remove(i);
            }

            if (tile.State != PlayAllTileState.Playing)
            {
                playing.Remove(i);
            }
        }

        return actions;
    }

    // A tile playing whose picture does not move: a dropped connection that never says so.
    private bool StoodStillTooLong(
        int tile,
        DateTimeOffset? at,
        TimeSpan elapsed)
    {
        if (at is not { } moment)
        {
            return false;
        }

        var still = playing.TryGetValue(tile, out var last) && last.At == moment
            ? last.Still + elapsed
            : TimeSpan.Zero;
        playing[tile] = (moment, still);
        return still > GiveUpAfter;
    }

    private bool LoadedTooLong(
        int tile,
        TimeSpan elapsed)
    {
        var total = loading.GetValueOrDefault(tile) + elapsed;
        loading[tile] = total;
        return total > GiveUpAfter;
    }
}