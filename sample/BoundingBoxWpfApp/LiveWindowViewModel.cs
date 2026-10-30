namespace BoundingBoxWpfApp;

/// <summary>
/// The live clock: the video streamed to the engine's player as a camera sends it, shown by
/// <c>VideoHost</c> with the boxes in its overlay window, and its pictures taken from the player
/// at the analysis rate and given to the detector, as CameraWall does. What is analysed depends
/// on timing, so two runs differ a little; the score shows how the detector does at the player's
/// pace, and the deterministic clock stays the one to compare with the baseline.
/// </summary>
[SuppressMessage("", "S2325:Make properties static", Justification = "XAML binding requires instance properties")]
public sealed partial class LiveWindowViewModel : ViewModelBase, IDisposable
{
#if DEBUG
    private const string BuildNote = " (a Debug build: many times slower than the apps)";
#else
    private const string BuildNote = "";
#endif

    // A stream that sends nothing for this long after it started has ended.
    private static readonly TimeSpan Quiet = TimeSpan.FromSeconds(3);

    private readonly LiveSource source;
    private readonly string tool;
    private readonly string logFolder;
    private readonly LiveAnalysis analysis;
    private readonly DispatcherTimer timer;
    private readonly IVideoPlayerFactory factory;
    private LiveSender? stream;
    private DateTime lastNewPicture;
    private bool capturing;
    private bool ended;

    [ObservableProperty]
    private IVideoPlayer? player;

    [ObservableProperty]
    private IReadOnlyList<BoundingBox>? detectedBoxes;

    [ObservableProperty]
    private IReadOnlyList<BoundingBox>? truthBoxes;

    [ObservableProperty]
    private string status = "Starting…";

    [ObservableProperty]
    private string liveScore = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="LiveWindowViewModel"/> class.
    /// </summary>
    /// <param name="source">What is played.</param>
    /// <param name="rotation">How the player turns the picture, besides how a recording is stored.</param>
    /// <param name="tool">The FFmpeg command-line tool, which streams the video.</param>
    /// <param name="logFolder">Where the tool's output goes.</param>
    /// <param name="factory">Makes the player.</param>
    public LiveWindowViewModel(
        LiveSource source,
        VideoRotation rotation,
        string tool,
        string logFolder,
        IVideoPlayerFactory factory)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(factory);

        this.source = source;
        this.tool = tool;
        this.logFolder = logFolder;
        this.factory = factory;
        analysis = new LiveAnalysis(source, rotation);
        Title = string.Create(CultureInfo.InvariantCulture, $"Live - {source.Name}, {source.AnalysesPerSecond}/s, turned {(int)analysis.Rotation}°");
        timer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromSeconds(1 / source.AnalysesPerSecond) };
        timer.Tick += OnTick;
    }

    /// <summary>
    /// Gets the window's title.
    /// </summary>
    public string Title { get; }

    /// <summary>
    /// Gets the width the detector analyses (the boxes' pixels).
    /// </summary>
    public int AnalysisWidth
        => analysis.AnalysisWidth;

    /// <summary>
    /// Gets the height the detector analyses.
    /// </summary>
    public int AnalysisHeight
        => analysis.AnalysisHeight;

    /// <summary>
    /// Gets the width of the picture as shown (the truth's pixels).
    /// </summary>
    public int DisplayWidth
        => analysis.DisplayWidth;

    /// <summary>
    /// Gets the height of the picture as shown.
    /// </summary>
    public int DisplayHeight
        => analysis.DisplayHeight;

    /// <summary>
    /// Opens the player on a free UDP port, then starts the stream, so its first picture is not missed.
    /// </summary>
    public async Task StartAsync()
    {
        var created = factory.Create();
        created.SetRotation(analysis.Rotation);
        Player = created;
        var port = LiveSender.FreePort();
        var address = new Uri($"udp://127.0.0.1:{port.ToString(CultureInfo.InvariantCulture)}");
        _ = Task.Run(() =>
        {
            try
            {
                created.Open(address, new StreamOptions());
            }
            catch (Exception ex) when (ex is InvalidOperationException or IOException or AtcSoft.VideoEngine.FFmpeg.FFmpegException)
            {
                Log.Error(ex, "The player could not open the live stream {Address}", address);
            }
        });

        // The player listens once it opens; FFmpeg then sends from the video's first picture.
        await Task.Delay(TimeSpan.FromSeconds(1)).ConfigureAwait(true);
        stream = LiveSender.Start(tool, source.VideoPath, port, Path.Combine(logFolder, "live-ffmpeg.log"));
        lastNewPicture = DateTime.UtcNow;
        Status = "Playing";
        Log.Information("Playing {Name} live from {Video} to {Address}, turned {Rotation}", source.Name, source.VideoPath, address, analysis.Rotation);
        timer.Start();
    }

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop()
        => End("Stopped");

    private bool CanStop()
        => !ended;

    /// <inheritdoc />
    public void Dispose()
    {
        End("Closed");
        if (Player is { } closing)
        {
            Player = null;
            try
            {
                closing.Close();
            }
            finally
            {
                closing.Dispose();
            }
        }
    }

    private async void OnTick(
        object? sender,
        EventArgs e)
    {
        if (capturing || ended || Player is not { } playing)
        {
            return;
        }

        capturing = true;
        try
        {
            var grey = playing.State == PlayerState.Playing
                ? await playing.CaptureGreyAsync(analysis.AnalysisWidth, analysis.AnalysisHeight).ConfigureAwait(true)
                : null;
            if (grey is not null && analysis.Analyse(grey) is { } boxes)
            {
                lastNewPicture = DateTime.UtcNow;
                DetectedBoxes = boxes.Detected;
                TruthBoxes = boxes.Truth;
                ShowScore();
            }

            // A storybook's times start at 0, so its last picture is known; a recording ends when
            // its stream does (its times need not start at 0).
            var nearEnd = source.Truth is not null && source.Fps > 0 && analysis.LastSeconds >= source.DurationSeconds - (1 / source.Fps) - 1e-6;
            if (nearEnd || (stream is { HasEnded: true } && DateTime.UtcNow - lastNewPicture > Quiet) || DateTime.UtcNow - lastNewPicture > Quiet * 3)
            {
                End("Ended");
            }
        }
        catch (InvalidOperationException ex)
        {
            Log.Warning(ex, "Taking a live picture failed");
        }
        finally
        {
            capturing = false;
        }
    }

    private void ShowScore()
    {
        var score = analysis.Score;
        LiveScore = source.Truth is null
            ? string.Create(CultureInfo.InvariantCulture, $"{analysis.Pictures} pictures, tracks {score.TrackCount} (no truth) · {analysis.MeanMilliseconds:0.00} ms a picture{BuildNote}")
            : string.Create(CultureInfo.InvariantCulture, $"{score.Summary} · {analysis.Pictures} pictures · {analysis.MeanMilliseconds:0.00} ms a picture{BuildNote}");
    }

    private void End(string how)
    {
        if (ended)
        {
            return;
        }

        ended = true;
        timer.Stop();
        stream?.Dispose();
        stream = null;
        ShowScore();
        Status = string.Create(CultureInfo.InvariantCulture, $"{how} at {analysis.LastSeconds:0.00} s");
        Log.Information("Live {Name} {How} after {Pictures} pictures: {Score}", source.Name, how.ToLowerInvariant(), analysis.Pictures, LiveScore);
        CommandManager.InvalidateRequerySuggested();
    }
}