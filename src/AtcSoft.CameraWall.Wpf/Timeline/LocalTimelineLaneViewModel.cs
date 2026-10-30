namespace AtcSoft.CameraWall.Wpf.Timeline;

using ActivityEvent = AtcSoft.VideoSurveillance.Models.ActivityEvent;
using CameraTimeline = AtcSoft.VideoSurveillance.Models.CameraTimeline;
using CoverageSpan = AtcSoft.VideoSurveillance.Models.CoverageSpan;
using TimelineStretch = AtcSoft.VideoSurveillance.Models.TimelineStretch;

/// <summary>
/// One camera's lane on CameraWall's timeline: what the lane control draws, and a click that plays
/// from that moment.
/// </summary>
public sealed partial class LocalTimelineLaneViewModel : ViewModelBase
{
    private readonly Action<LocalTimelineLaneViewModel, double> playAt;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalTimelineLaneViewModel"/> class.
    /// </summary>
    /// <param name="cameraId">The camera.</param>
    /// <param name="cameraName">The camera's name.</param>
    /// <param name="timeline">The camera's timeline over the span.</param>
    /// <param name="from">The start of the span.</param>
    /// <param name="to">The end of the span.</param>
    /// <param name="now">Now, for the red line when the span reaches it.</param>
    /// <param name="playAt">Plays the lane from a place, as a fraction of the span.</param>
    public LocalTimelineLaneViewModel(
        Guid cameraId,
        string cameraName,
        CameraTimeline timeline,
        DateTimeOffset from,
        DateTimeOffset to,
        DateTimeOffset now,
        Action<LocalTimelineLaneViewModel, double> playAt)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        ArgumentNullException.ThrowIfNull(playAt);

        this.playAt = playAt;
        CameraId = cameraId;
        CameraName = cameraName;
        Coverage = timeline.Coverage;
        Stretches = timeline.Stretches;

        var length = (to - from).TotalSeconds;
        EventBands = length <= 0
            ? []
            : [.. timeline.Events.Select(e => new ActivityEvent(
                Math.Clamp((e.Start - from).TotalSeconds / length, 0, 1),
                Math.Clamp((e.End - from).TotalSeconds / length, 0, 1)))];
        NowFraction = now >= from && now <= to && length > 0 ? (now - from).TotalSeconds / length : -1;
    }

    public Guid CameraId { get; }

    public string CameraName { get; }

    public IReadOnlyList<CoverageSpan> Coverage { get; }

    public IReadOnlyList<TimelineStretch> Stretches { get; }

    public IReadOnlyList<ActivityEvent> EventBands { get; }

    public double NowFraction { get; }

    [RelayCommand]
    private void PlayAt(double fraction)
        => playAt(this, fraction);
}