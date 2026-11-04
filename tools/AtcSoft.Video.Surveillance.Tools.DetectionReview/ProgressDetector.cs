namespace AtcSoft.Video.Surveillance.Tools.DetectionReview;

/// <summary>
/// The detector, telling how far into the recording it has come: the analysis is most of the
/// time a review takes, and it gives no word of its own until it is done.
/// </summary>
internal sealed class ProgressDetector(
    AtcSoft.Video.Surveillance.Services.IMotionDetector inner,
    TimeSpan duration,
    IProgress<ReviewProgress> progress) : AtcSoft.Video.Surveillance.Services.IMotionDetector
{
    private DateTimeOffset? start;
    private int lastSecond = -1;

    public AtcSoft.Video.Surveillance.Models.MotionDetectorResult Process(
        AtcSoft.Video.Surveillance.Models.MotionFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);

        start ??= frame.Time;
        var seconds = (int)(frame.Time - start.Value).TotalSeconds;
        if (seconds != lastSecond)
        {
            lastSecond = seconds;
            var total = Math.Max(seconds, (int)Math.Ceiling(duration.TotalSeconds));
            progress.Report(new ReviewProgress(
                string.Create(CultureInfo.InvariantCulture, $"Running the detector: {seconds} of {total} s"),
                seconds,
                Math.Max(1, total)));
        }

        return inner.Process(frame);
    }

    public AtcSoft.Video.Surveillance.Models.MotionDetectorResult Idle(
        DateTimeOffset now)
        => inner.Idle(now);

    public void Reset()
    {
        start = null;
        lastSecond = -1;
        inner.Reset();
    }

    public void UseZones(
        IReadOnlyList<AtcSoft.Video.Surveillance.Models.MotionZone> zones)
        => inner.UseZones(zones);
}