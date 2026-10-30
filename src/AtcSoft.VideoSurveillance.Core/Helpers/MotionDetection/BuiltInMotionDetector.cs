namespace AtcSoft.VideoSurveillance.Helpers.MotionDetection;

/// <summary>
/// The motion detector of the bounding-box plan, in plain C#: a light blur, a learnt background
/// per pixel (mean and variance), a lighting-change check, morphology on the foreground, blobs,
/// merging of near blobs, and a tracker. Motion is on while a confirmed track exists: something
/// that has been seen for a while and has travelled. A tree in the wind sways on the spot and
/// never confirms; a slow walker differs from the background as a whole figure. A camera's ignore
/// zones are never foreground and do not count towards the changed share, so the activity level
/// and the lighting check leave them out too. A watch zone has its own sensitivity (its pixels'
/// thresholds) and smallest object (for a blob whose middle is in it); without watch zones the
/// detector works as it did before they existed.
/// </summary>
public sealed class BuiltInMotionDetector : IMotionDetector
{
    // Above this share of the picture foreground, the scene changed rather than something in it.
    private const double LargeChangeShare = 0.5;

    private readonly MotionDetectorOptions options;
    private readonly MotionHold hold;
    private readonly BackgroundModel background = new();
    private readonly MotionTracker tracker = new();
    private readonly DetectorBuffers buffers = new();
    private IReadOnlyList<MotionZone> zones;
    private IReadOnlyList<MotionZone>? ignoredFor;
    private byte[]? ignored;
    private int ignoredWidth;
    private int ignoredHeight;
    private byte[]? watched;
    private IReadOnlyList<MotionZone> watchZones = [];
    private double[] zoneSigmas = [];
    private double[] zoneMinThresholds = [];
    private double[] zoneSmallestShares = [];
    private DateTimeOffset? lastTime;
    private DateTimeOffset? settleUntil;
    private bool settleIsWarmUp;
    private bool lastHadBoxes;
    private bool holding;
    private double lastShare;

    /// <summary>
    /// Initializes a new instance of the <see cref="BuiltInMotionDetector"/> class.
    /// </summary>
    /// <param name="settings">The camera's motion settings.</param>
    /// <param name="options">The tuning; from <paramref name="settings"/> when not given.</param>
    /// <param name="zones">The camera's motion zones: ignored, or watched with their own values.</param>
    public BuiltInMotionDetector(
        MotionDetectionSettings settings,
        MotionDetectorOptions? options = null,
        IReadOnlyList<MotionZone>? zones = null)
    {
        ArgumentNullException.ThrowIfNull(settings);

        this.zones = zones is null ? [] : [.. zones.Select(z => z.Clone())];
        this.options = options ?? MotionDetectorOptions.From(settings);
        hold = new MotionHold(TimeSpan.FromSeconds(Math.Max(0, settings.PostMotionDurationSeconds)));
    }

    /// <summary>
    /// Gets how long after its first picture the detector only learns the scene and shows no
    /// box (0 by default). A recording read from its start sets it: until the background has
    /// been learnt, swaying things and the scene itself give wrong boxes. A live camera leaves it
    /// at 0, as it runs all day and something at its start should not be missed.
    /// </summary>
    public double WarmUpSeconds { get; init; }

    /// <summary>
    /// Gets whether each picture leaves what the detector saw in <see cref="LastDiagnostics"/>:
    /// for a person tuning it (the bounding box sample). Off by default, and off it costs nothing.
    /// </summary>
    public bool KeepDiagnostics { get; init; }

    /// <summary>
    /// Gets what the detector saw in the last picture, when <see cref="KeepDiagnostics"/> is on.
    /// </summary>
    public MotionDiagnostics? LastDiagnostics { get; private set; }

    /// <inheritdoc />
    public MotionDetectorResult Process(MotionFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        buffers.EnsurePixels(frame.Gray.Length);
        var blurred = buffers.Blurred;
        MaskOperations.Blur3(frame.Gray, blurred, buffers.Rows, frame.Width, frame.Height);
        var dt = lastTime is { } last ? Math.Max(0, (frame.Time - last).TotalSeconds) : 0;
        lastTime = frame.Time;

        if (background.IsEmpty || background.Width != frame.Width || background.Height != frame.Height)
        {
            background.Reset(blurred, frame.Width, frame.Height);
            tracker.Reset();
            holding = false;
            settleUntil = WarmUpSeconds > 0 ? frame.Time.AddSeconds(WarmUpSeconds) : null;
            settleIsWarmUp = true;
            Diagnose(MotionDiagnosticsStage.Learning, frame, mask: null, []);
            return Result(moving: false, frame.Time, 0, []);
        }

        var mask = buffers.Mask;
        var ignore = Ignored(frame.Width, frame.Height);

        // Most of the picture still differs (a change of light taken for motion, as lighting
        // changes are not ignored): it is learnt at the background's speed, not kept like
        // something that stopped, so motion is seen again within seconds.
        var foregroundSeconds = lastShare > LargeChangeShare ? options.BackgroundSeconds : options.ForegroundSeconds;
        var share = ApplyBackground(
            blurred,
            mask,
            Rate(dt, options.BackgroundSeconds),
            Rate(dt, foregroundSeconds),
            ignore);
        lastShare = share;

        // Most of the picture changed at once: light (exposure, a cloud, dusk), not an object.
        // Shift the background by the overall change, so what moves stays foreground; if the
        // change was not even (the IR switch from colour to grey), learn the picture anew.
        if (share > options.LightingChangeShare)
        {
            background.ShiftTo(blurred);
            share = ApplyBackground(blurred, mask, 0, 0, ignore);
            lastShare = share;
            if (share > options.LightingChangeShare)
            {
                background.Reset(blurred, frame.Width, frame.Height);
                tracker.Reset();
                holding = false;
            }

            settleUntil = frame.Time.AddSeconds(options.LightingSettleSeconds);
            settleIsWarmUp = false;
            Diagnose(MotionDiagnosticsStage.LightingChange, frame, mask: null, []);
            return Result(moving: false, frame.Time, share * 100, []);
        }

        if (settleUntil is { } settle && frame.Time < settle)
        {
            Diagnose(settleIsWarmUp ? MotionDiagnosticsStage.WarmUp : MotionDiagnosticsStage.Settling, frame, mask: null, []);
            return Result(moving: false, frame.Time, share * 100, []);
        }

        MaskOperations.Open(mask, frame.Width, frame.Height, radius: 1, buffers.Temp);
        MaskOperations.Close(mask, frame.Width, frame.Height, radius: 3, buffers.Temp);

        var area = frame.Width * frame.Height;
        var found = ConnectedComponents.Find(mask, frame.Width, frame.Height, buffers);
        var blobs = found
            .Where(b => b.Pixels >= SmallestShare(b, frame.Width, frame.Height) * area)
            .ToList();
        var tooSmall = KeepDiagnostics ? found.Except(blobs).ToList() : null;
        blobs = ConnectedComponents.Merge(blobs, (int)Math.Ceiling(options.MergeGapShare * frame.Width));

        var time = (frame.Time - DateTimeOffset.UnixEpoch).TotalSeconds;
        var seen = KeepDiagnostics ? new List<(Blob Box, int Id, MotionCandidateState State, double Travel, bool Certain)>() : null;
        var tracks = tracker.Update(blobs, time, options, frame.Width, seen);
        if (seen is not null)
        {
            MotionCandidate[] candidates =
            [
                .. tooSmall!.Select(b => new MotionCandidate(ToBox(b), MotionCandidateState.TooSmall, TrackId: null, Travel: 0, Held: false)),
                .. seen.Select(t => new MotionCandidate(ToBox(t.Box), t.State, t.Id, t.Travel, t.Certain)),
            ];
            Diagnose(MotionDiagnosticsStage.Analysed, frame, mask, candidates);
        }

        Hold(tracks, frame.Width);
        var boxes = tracks
            .Select(t => new BoundingBox { X = t.Box.MinX, Y = t.Box.MinY, Width = t.Box.Width, Height = t.Box.Height, TrackId = t.Id, Zone = ZoneOf(t.Box, frame.Width, frame.Height) })
            .OrderByDescending(b => b.Area)
            .ToList();

        return Result(boxes.Count > 0, frame.Time, share * 100, boxes);
    }

    /// <inheritdoc />
    public MotionDetectorResult Idle(DateTimeOffset now)
        => Result(moving: false, now, 0, []);

    /// <inheritdoc />
    public void Reset()
    {
        background.Reset([], 0, 0);
        tracker.Reset();
        holding = false;
        lastTime = null;
        settleUntil = null;
        lastHadBoxes = false;
        lastShare = 0;
        LastDiagnostics = null;
    }

    /// <inheritdoc />
    public void UseZones(IReadOnlyList<MotionZone> zones)
    {
        ArgumentNullException.ThrowIfNull(zones);

        // A copy, so a later edit of the camera's list is not half seen; the mask follows on the next picture.
        Volatile.Write(ref this.zones, [.. zones.Select(z => z.Clone())]);
    }

    // The zones at this size, filled in once until the size or the zones change: the ignored
    // pixels, and the watch zone of each pixel with each zone's thresholds and smallest object
    // (index 0 is the camera's).
    private byte[]? Ignored(
        int width,
        int height)
    {
        var current = Volatile.Read(ref zones);
        if (ignoredWidth != width || ignoredHeight != height || !ReferenceEquals(ignoredFor, current))
        {
            ignored = ZoneMask.Ignored(current, width, height);
            watched = ZoneMask.Watched(current, width, height);
            var watch = ZoneMask.WatchZones(current);
            watchZones = watch;
            var camera = (ThresholdSigmas: options.ThresholdSigmas, MinThreshold: options.MinThreshold);
            var thresholds = watch.Select(z => z.Sensitivity is { } s ? MotionDetectorOptions.Thresholds(s) : camera).Prepend(camera).ToList();
            zoneSigmas = [.. thresholds.Select(t => t.ThresholdSigmas)];
            zoneMinThresholds = [.. thresholds.Select(t => t.MinThreshold)];
            zoneSmallestShares =
            [
                options.SmallestObjectShare,
                .. watch.Select(z => z.SmallestObjectPercent is { } percent ? Math.Clamp(percent, 0, 100) / 100 : options.SmallestObjectShare),
            ];
            ignoredFor = current;
            ignoredWidth = width;
            ignoredHeight = height;
        }

        return ignored;
    }

    // Without watch zones, the one threshold of the camera, as before they existed.
    private double ApplyBackground(
        byte[] blurred,
        byte[] mask,
        double backgroundRate,
        double foregroundRate,
        byte[]? ignore)
        => watched is { } zoneOf
            ? background.ApplyZoned(blurred, mask, zoneSigmas, zoneMinThresholds, zoneOf, backgroundRate, foregroundRate, ignore, Held)
            : background.Apply(blurred, mask, options.ThresholdSigmas, options.MinThreshold, backgroundRate, foregroundRate, ignore, Held);

    private byte[]? Held
        => holding ? buffers.Held : null;

    // Marks the boxes of the objects shown that have travelled for certain, so the background does
    // not take their swinging limbs in as wind in the next picture. Not every track: a false one on
    // a branch would keep its own pixels from learning the sway, and so keep itself alive.
    private void Hold(
        List<(Blob Box, int Id, bool Certain)> tracks,
        int width)
    {
        if (holding)
        {
            Array.Clear(buffers.Held);
        }

        holding = false;
        foreach (var (box, _, _) in tracks.Where(t => t.Certain))
        {
            holding = true;
            for (var y = box.MinY; y <= box.MaxY; y++)
            {
                Array.Fill(buffers.Held, (byte)1, (y * width) + box.MinX, box.Width);
            }
        }
    }

    // Keeps what the detector saw in this picture, when asked to: the mask packed to a bit a pixel.
    private void Diagnose(
        MotionDiagnosticsStage stage,
        MotionFrame frame,
        byte[]? mask,
        IReadOnlyList<MotionCandidate> candidates)
    {
        if (!KeepDiagnostics)
        {
            return;
        }

        var pixels = frame.Width * frame.Height;
        LastDiagnostics = new MotionDiagnostics(
            stage,
            frame.Width,
            frame.Height,
            mask is null ? new byte[(pixels + 7) / 8] : MotionDiagnostics.Pack(mask, pixels),
            ignoredWidth == frame.Width && ignoredHeight == frame.Height ? ignored : null,
            candidates);
    }

    private static BoundingBox ToBox(Blob blob)
        => new() { X = blob.MinX, Y = blob.MinY, Width = blob.Width, Height = blob.Height };

    // The name of the watch zone a box's middle is in, or null.
    private string? ZoneOf(
        Blob box,
        int width,
        int height)
    {
        if (watched is not { } zoneOf)
        {
            return null;
        }

        var x = Math.Clamp((int)box.CenterX, 0, width - 1);
        var y = Math.Clamp((int)box.CenterY, 0, height - 1);
        var zone = zoneOf[(y * width) + x];
        return zone == 0 ? null : watchZones[zone - 1].Name;
    }

    // The smallest object for a blob: that of the watch zone its middle is in, else the camera's.
    private double SmallestShare(
        Blob blob,
        int width,
        int height)
    {
        if (watched is not { } zoneOf)
        {
            return options.SmallestObjectShare;
        }

        var x = Math.Clamp((int)blob.CenterX, 0, width - 1);
        var y = Math.Clamp((int)blob.CenterY, 0, height - 1);
        return zoneSmallestShares[zoneOf[(y * width) + x]];
    }

    // How much a pixel learns in dt for a time constant: the share of the way to the new value.
    private static double Rate(
        double dt,
        double timeConstant)
        => timeConstant <= 0 ? 1 : 1 - Math.Exp(-dt / timeConstant);

    // While motion is held, new boxes are reported, and once more when they went away (empty),
    // so a box is not left on screen until motion ends; quiet pictures in between report nothing.
    private MotionDetectorResult Result(
        bool moving,
        DateTimeOffset time,
        double changePercent,
        IReadOnlyList<BoundingBox> boxes)
    {
        var change = hold.Update(moving, time);
        if (change == MotionHoldChange.None && hold.IsActive && lastHadBoxes)
        {
            change = MotionHoldChange.Continues;
        }

        lastHadBoxes = boxes.Count > 0 && change != MotionHoldChange.Ended;
        return new MotionDetectorResult(hold.IsActive, change, changePercent, change == MotionHoldChange.Ended ? [] : boxes);
    }
}