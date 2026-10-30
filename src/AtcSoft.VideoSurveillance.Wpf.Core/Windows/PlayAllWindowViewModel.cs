namespace AtcSoft.VideoSurveillance.Wpf.Core.Windows;

/// <summary>
/// *Play all from here*: every camera's tile from one moment, kept roughly in step by
/// <see cref="PlayAllPacer"/>, with one pause for all.
/// </summary>
public sealed partial class PlayAllWindowViewModel : ViewModelBase, IDisposable
{
    /// <summary>The most tiles a window shows.</summary>
    public const int MaxTiles = 9;

    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(250);

    private readonly PlayAllPacer pacer;
    private readonly System.Diagnostics.Stopwatch sinceTick = new();
    private DispatcherTimer? timer;

    [ObservableProperty]
    private string clockText;

    [ObservableProperty(DependentPropertyNames = [nameof(PlayPauseText)])]
    private bool isPaused;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlayAllWindowViewModel"/> class.
    /// </summary>
    /// <param name="moment">The moment every tile plays from.</param>
    /// <param name="tiles">The cameras' tiles; only the first <see cref="MaxTiles"/> are shown.</param>
    public PlayAllWindowViewModel(
        DateTimeOffset moment,
        IReadOnlyList<PlayAllTileViewModel> tiles)
    {
        ArgumentNullException.ThrowIfNull(tiles);

        pacer = new PlayAllPacer(moment);
        Tiles = [.. tiles.Take(MaxTiles)];
        Title = string.Format(CultureInfo.CurrentCulture, Translations.PlayAllFrom1, FormatClock(moment));
        LimitText = tiles.Count > MaxTiles
            ? string.Format(CultureInfo.CurrentCulture, Translations.PlayAllShowsFirst1, MaxTiles)
            : string.Empty;
        Columns = (int)Math.Ceiling(Math.Sqrt(Math.Max(Tiles.Count, 1)));
        clockText = FormatClock(moment);
    }

    public string Title { get; }

    public string LimitText { get; }

    public IReadOnlyList<PlayAllTileViewModel> Tiles { get; }

    public int Columns { get; }

    public string PlayPauseText
        => IsPaused ? Translations.Play : Translations.Pause;

    /// <summary>
    /// Starts the clock, once each tile has its player.
    /// </summary>
    public void Start()
    {
        if (timer is not null)
        {
            return;
        }

        Step(TimeSpan.Zero);
        sinceTick.Restart();
        timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TickInterval };
        timer.Tick += OnTick;
        timer.Start();
    }

    /// <summary>
    /// Moves the clock on and has every tile do what the pacer asks.
    /// </summary>
    /// <param name="elapsed">The real time since the last step.</param>
    public void Step(TimeSpan elapsed)
    {
        var actions = pacer.Tick(elapsed, [.. Tiles.Select(t => t.Report())]);
        for (var i = 0; i < Tiles.Count; i++)
        {
            Tiles[i].Apply(actions[i], pacer.Clock);
        }

        ClockText = FormatClock(pacer.Clock);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (timer is not null)
        {
            timer.Stop();
            timer.Tick -= OnTick;
            timer = null;
        }

        foreach (var tile in Tiles)
        {
            tile.Close();
        }
    }

    private static string FormatClock(DateTimeOffset moment)
        => moment.ToLocalTime().ToString("G", CultureInfo.CurrentCulture);

    [RelayCommand]
    private void PlayPause()
    {
        IsPaused = !IsPaused;
        pacer.IsPaused = IsPaused;
        foreach (var tile in Tiles)
        {
            tile.SetPaused(IsPaused);
        }
    }

    private void OnTick(
        object? sender,
        EventArgs e)
    {
        var elapsed = sinceTick.Elapsed;
        sinceTick.Restart();
        Step(elapsed);
    }
}