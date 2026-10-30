namespace BoundingBoxWpfApp;

/// <summary>
/// The live clock's analysis: each grey picture the player gives goes through the detector by its
/// time in the stream, as in the apps; the boxes it shows are kept as the tiles keep them, and are
/// scored against the truth of that time, turned as the player turns the picture.
/// </summary>
public sealed class LiveAnalysis
{
    private static readonly DateTimeOffset Epoch = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private readonly LiveSource source;
    private readonly BuiltInMotionDetector detector;
    private readonly List<ScoredFrame> frames = [];
    private IReadOnlyList<BoundingBox> shown = [];
    private TimeSpan? lastPosition;

    /// <summary>
    /// Initializes a new instance of the <see cref="LiveAnalysis"/> class.
    /// </summary>
    /// <param name="source">What is played.</param>
    /// <param name="rotation">How the picture is turned besides how it is stored.</param>
    public LiveAnalysis(
        LiveSource source,
        VideoRotation rotation)
    {
        ArgumentNullException.ThrowIfNull(source);

        this.source = source;
        Rotation = (VideoRotation)(((int)source.StoredRotation + (int)rotation) % 360);
        var turned = Rotation is VideoRotation.Rotate90 or VideoRotation.Rotate270;
        DisplayWidth = turned ? source.VideoHeight : source.VideoWidth;
        DisplayHeight = turned ? source.VideoWidth : source.VideoHeight;
        (AnalysisWidth, AnalysisHeight) = MotionAnalysisSize.For(
            source.Detector.Settings.AnalysisWidth > 0 ? source.Detector.Settings.AnalysisWidth : MotionAnalysisSize.DefaultLongerSide,
            DisplayWidth,
            DisplayHeight);
        detector = source.Detector.Create(keepDiagnostics: false);
    }

    /// <summary>
    /// Gets how the player turns the picture: the stored turn and the one chosen.
    /// </summary>
    public VideoRotation Rotation { get; }

    /// <summary>
    /// Gets the width of the picture as shown.
    /// </summary>
    public int DisplayWidth { get; }

    /// <summary>
    /// Gets the height of the picture as shown.
    /// </summary>
    public int DisplayHeight { get; }

    /// <summary>
    /// Gets the width the detector analyses.
    /// </summary>
    public int AnalysisWidth { get; }

    /// <summary>
    /// Gets the height the detector analyses.
    /// </summary>
    public int AnalysisHeight { get; }

    /// <summary>
    /// Gets how many pictures were analysed.
    /// </summary>
    public int Pictures
        => frames.Count;

    /// <summary>
    /// Gets the time in the stream of the last picture analysed.
    /// </summary>
    public double LastSeconds
        => lastPosition?.TotalSeconds ?? 0;

    /// <summary>
    /// Gets the score so far: against the truth for a storybook, for a recording only the boxes
    /// (all false, as there is no truth).
    /// </summary>
    public StorybookScore Score
        => StorybookScorer.Score(frames, source.Expect);

    /// <summary>
    /// Gets the detector's mean time a picture so far, in milliseconds.
    /// </summary>
    public double MeanMilliseconds
        => frames.Count == 0 ? 0 : frames.Average(f => f.DetectorMilliseconds);

    /// <summary>
    /// Analyses a picture from the player, unless it is the one analysed last.
    /// </summary>
    /// <param name="grey">The picture, at <see cref="AnalysisWidth"/> by <see cref="AnalysisHeight"/>, turned as shown.</param>
    /// <returns>The boxes shown (analysed pixels) and the objects to box (shown pixels), or <see langword="null"/> for the same picture again.</returns>
    public (IReadOnlyList<BoundingBox> Detected, IReadOnlyList<BoundingBox> Truth)? Analyse(
        GreyFrame grey)
    {
        ArgumentNullException.ThrowIfNull(grey);

        if (grey.Position == lastPosition || grey.Width != AnalysisWidth || grey.Height != AnalysisHeight)
        {
            return null;
        }

        lastPosition = grey.Position;
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var result = detector.Process(new MotionFrame(grey.Pixels, AnalysisWidth, AnalysisHeight, Epoch.Add(grey.Position)));
        var milliseconds = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        shown = result.Change switch
        {
            MotionHoldChange.Started or MotionHoldChange.Continues => result.Boxes,
            MotionHoldChange.Ended => [],
            _ => result.IsActive ? shown : [],
        };

        var scaleX = DisplayWidth / (double)AnalysisWidth;
        var scaleY = DisplayHeight / (double)AnalysisHeight;
        var (truth, ignoreAll) = TruthAt(grey.Position.TotalSeconds);
        frames.Add(new ScoredFrame(
            grey.Position.TotalSeconds,
            truth,
            ignoreAll,
            [.. shown.Select(b => new DetectedBox(new PixelBox(b.X * scaleX, b.Y * scaleY, b.Width * scaleX, b.Height * scaleY), b.TrackId))],
            DetectorMilliseconds: milliseconds));

        IReadOnlyList<BoundingBox> objects = [.. truth
            .Where(t => !t.IsDistractor && t.State == TruthState.MustBox)
            .Select(t => new BoundingBox { X = t.Box.X, Y = t.Box.Y, Width = t.Box.Width, Height = t.Box.Height })];
        return (shown, objects);
    }

    /// <summary>
    /// Turns a box of the stored picture as the player turns the picture (clockwise).
    /// </summary>
    /// <param name="box">The box, in the stored picture's pixels.</param>
    /// <param name="storedWidth">The stored picture's width.</param>
    /// <param name="storedHeight">The stored picture's height.</param>
    /// <param name="rotation">The turn.</param>
    /// <returns>The box in the shown picture's pixels.</returns>
    public static PixelBox Turn(
        PixelBox box,
        int storedWidth,
        int storedHeight,
        VideoRotation rotation)
    {
        // A turn of a quarter swaps the box's sides.
        var (x, y, across, down) = (box.X, box.Y, box.Width, box.Height);
        return rotation switch
        {
            VideoRotation.Rotate90 => new PixelBox(storedHeight - (y + down), x, down, across),
            VideoRotation.Rotate180 => new PixelBox(storedWidth - (x + across), storedHeight - (y + down), across, down),
            VideoRotation.Rotate270 => new PixelBox(y, storedWidth - (x + across), down, across),
            _ => box,
        };
    }

    // The truth of the frame at a time, turned as shown; none for a recording.
    private (IReadOnlyList<TruthBox> Truth, bool IgnoreAll) TruthAt(
        double seconds)
    {
        if (source.Truth is not { Count: > 0 } truth)
        {
            return ([], false);
        }

        var frame = truth[Math.Clamp((int)Math.Round(seconds * source.Fps), 0, truth.Count - 1)];
        return ([.. frame.Truth.Select(t => t with { Box = Turn(t.Box, source.VideoWidth, source.VideoHeight, Rotation) })], frame.IgnoreAll);
    }
}