namespace AtcSoft.VideoSurveillance.Helpers.Activity;

/// <summary>
/// Follows one recording's analysed pictures and says when activity starts (a confirmed track
/// appears) and ends (nothing moved for <see cref="ActivitySummarizer.MergeGapSeconds"/>), the
/// same way the summaries join moving seconds into events. Not thread-safe: one recording's
/// pictures come one at a time.
/// </summary>
public sealed class ActivityTracker
{
    private readonly Guid cameraId;
    private readonly string videoPath;
    private readonly DateTimeOffset recordingStart;
    private DateTimeOffset? start;
    private DateTimeOffset lastMoving;
    private double peakLevel;
    private int tracks;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActivityTracker"/> class.
    /// </summary>
    /// <param name="cameraId">The camera.</param>
    /// <param name="videoPath">The recording.</param>
    /// <param name="recordingStart">When the recording started, for the offsets.</param>
    public ActivityTracker(
        Guid cameraId,
        string videoPath,
        DateTimeOffset recordingStart)
    {
        this.cameraId = cameraId;
        this.videoPath = videoPath;
        this.recordingStart = recordingStart;
    }

    /// <summary>
    /// Gets a value indicating whether activity goes on.
    /// </summary>
    public bool IsActive
        => start is not null;

    /// <summary>
    /// Adds the result of one analysed picture.
    /// </summary>
    /// <param name="time">When the picture was taken.</param>
    /// <param name="result">What the detector found in it.</param>
    /// <returns>The change, or <see langword="null"/> when activity neither started nor ended.</returns>
    public CameraActivityChangedEventArgs? Add(
        DateTimeOffset time,
        MotionDetectorResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.Boxes.Count > 0)
        {
            lastMoving = time;
            peakLevel = Math.Max(peakLevel, result.ChangePercent);
            tracks = Math.Max(tracks, result.Boxes.Count);
            if (start is null)
            {
                start = time;
                return Change(isActive: true, end: null);
            }

            return null;
        }

        return start is not null && (time - lastMoving).TotalSeconds > ActivitySummarizer.MergeGapSeconds
            ? End()
            : null;
    }

    /// <summary>
    /// Ends activity that goes on, as when the recording ends.
    /// </summary>
    /// <returns>The end, or <see langword="null"/> when nothing was going on.</returns>
    public CameraActivityChangedEventArgs? End()
    {
        if (start is null)
        {
            return null;
        }

        var ended = Change(isActive: false, end: lastMoving.AddSeconds(1));
        start = null;
        peakLevel = 0;
        tracks = 0;
        return ended;
    }

    private CameraActivityChangedEventArgs Change(
        bool isActive,
        DateTimeOffset? end)
        => new(
            cameraId,
            isActive,
            start!.Value,
            end,
            videoPath,
            Math.Max((start.Value - recordingStart).TotalSeconds, 0),
            peakLevel,
            tracks);
}