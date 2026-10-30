namespace AtcSoft.VideoSurveillance.Helpers;

/// <summary>
/// Today's motion detection behind <see cref="IMotionDetector"/>: each picture is compared with
/// the one before (<see cref="MotionFrameAnalyzer"/>), motion is on when the share of changed
/// pixels reaches <see cref="MotionDetectionSettings.MinimumChangePercent"/>, and it stays on for
/// the post-motion time (<see cref="MotionHold"/>). The baseline the bounding-box plan improves on.
/// </summary>
public sealed class FrameDifferenceMotionDetector : IMotionDetector
{
    private readonly MotionDetectionSettings settings;
    private readonly MotionHold hold;
    private MotionFrame? previous;

    /// <summary>
    /// Initializes a new instance of the <see cref="FrameDifferenceMotionDetector"/> class.
    /// </summary>
    /// <param name="settings">The camera's motion settings.</param>
    public FrameDifferenceMotionDetector(MotionDetectionSettings settings)
    {
        this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
        hold = new MotionHold(TimeSpan.FromSeconds(Math.Max(0, settings.PostMotionDurationSeconds)));
    }

    /// <inheritdoc />
    public MotionDetectorResult Process(MotionFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        var before = previous;
        previous = frame;
        if (before is null || before.Width != frame.Width || before.Height != frame.Height)
        {
            return MotionDetectorResult.Quiet with { IsActive = hold.IsActive };
        }

        var analysis = MotionFrameAnalyzer.Analyze(before.Gray, frame.Gray, frame.Width, frame.Height, settings);
        var moving = analysis.ChangePercent > 0 && analysis.ChangePercent >= settings.MinimumChangePercent;
        var change = hold.Update(moving, frame.Time);
        var boxes = change is MotionHoldChange.Started or MotionHoldChange.Continues
            ? analysis.BoundingBoxes
            : [];

        return new MotionDetectorResult(hold.IsActive, change, analysis.ChangePercent, boxes);
    }

    /// <inheritdoc />
    public MotionDetectorResult Idle(DateTimeOffset now)
    {
        var change = hold.Update(moving: false, now);
        return new MotionDetectorResult(hold.IsActive, change, 0, []);
    }

    /// <inheritdoc />
    public void Reset()
        => previous = null;
}