namespace AtcSoft.Video.Surveillance.CameraWall.Wpf.Timeline;

using ActivityEvent = AtcSoft.Video.Surveillance.Models.ActivityEvent;
using CameraTimeline = AtcSoft.Video.Surveillance.Models.CameraTimeline;
using CoverageSpan = AtcSoft.Video.Surveillance.Models.CoverageSpan;
using GapReason = AtcSoft.Video.Surveillance.Enums.GapReason;
using TimelineGap = AtcSoft.Video.Surveillance.Models.TimelineGap;
using TimelineGapBand = AtcSoft.Video.Surveillance.Models.TimelineGapBand;
using TimelineStretch = AtcSoft.Video.Surveillance.Models.TimelineStretch;

/// <summary>
/// One camera's lane on CameraWall's timeline: what the lane control draws (a gap coloured by why
/// nothing was recorded, its times under the pointer), how often the camera dropped out beside its
/// name, and a click that plays from that moment.
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
    /// <param name="gaps">The gaps with their reasons; without them the gaps are plain grey.</param>
    /// <param name="drops">How often the camera dropped out in the span.</param>
    /// <param name="offline">How long the camera was offline in the span.</param>
    public LocalTimelineLaneViewModel(
        Guid cameraId,
        string cameraName,
        CameraTimeline timeline,
        DateTimeOffset from,
        DateTimeOffset to,
        DateTimeOffset now,
        Action<LocalTimelineLaneViewModel, double> playAt,
        IReadOnlyList<TimelineGap>? gaps = null,
        int drops = 0,
        TimeSpan offline = default)
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
            :
            [
                .. timeline.Events.Select(e => new ActivityEvent(
                    Math.Clamp((e.Start - from).TotalSeconds / length, 0, 1),
                    Math.Clamp((e.End - from).TotalSeconds / length, 0, 1))),
            ];
        NowFraction = now >= from && now <= to && length > 0 ? (now - from).TotalSeconds / length : -1;
        GapBands = gaps is null || length <= 0
            ? null
            :
            [
                .. gaps
                    .Select(g => new TimelineGapBand(
                        Math.Clamp((g.Start - from).TotalSeconds / length, 0, 1),
                        Math.Clamp((g.End - from).TotalSeconds / length, 0, 1),
                        g.Reason,
                        GapText(g)))
                    .Where(g => g.End > g.Start),
            ];
        OfflineText = drops > 0 || offline >= TimeSpan.FromMinutes(1)
            ? string.Format(CultureInfo.CurrentCulture, Translations.OfflineSummary2, drops, FormatDuration(offline))
            : string.Empty;
    }

    public Guid CameraId { get; }

    public string CameraName { get; }

    public IReadOnlyList<CoverageSpan> Coverage { get; }

    public IReadOnlyList<TimelineStretch> Stretches { get; }

    public IReadOnlyList<ActivityEvent> EventBands { get; }

    public double NowFraction { get; }

    /// <summary>
    /// Gets the gaps with their reasons, or <see langword="null"/> when no history explains them.
    /// </summary>
    public IReadOnlyList<TimelineGapBand>? GapBands { get; }

    /// <summary>
    /// Gets how often and how long the camera was offline in the span; empty when it was not.
    /// </summary>
    public string OfflineText { get; }

    /// <summary>
    /// Gets a value indicating whether the camera was offline in the span.
    /// </summary>
    public bool HasOffline
        => OfflineText.Length > 0;

    /// <summary>
    /// Writes a length the way a lane tells it: <c>45 min</c>, <c>2 h 5 min</c>.
    /// </summary>
    /// <param name="length">The length.</param>
    public static string FormatDuration(TimeSpan length)
    {
        var minutes = (int)Math.Round(Math.Max(length.TotalMinutes, 0));
        return minutes < 60
            ? string.Format(CultureInfo.CurrentCulture, Translations.DurationMinutes1, Math.Max(minutes, length > TimeSpan.Zero ? 1 : 0))
            : string.Format(CultureInfo.CurrentCulture, Translations.DurationHoursMinutes2, minutes / 60, minutes % 60);
    }

    /// <summary>
    /// Gets the text of a gap's reason in CameraWall: its own name where the server's says server.
    /// </summary>
    /// <param name="reason">Why nothing was recorded.</param>
    public static string ReasonText(GapReason reason)
        => reason == GapReason.ServerDown
            ? Translations.GapCameraWallNotRunning
            : AtcSoft.Video.Surveillance.Wpf.Core.UserControls.TimelineGapLook.TextOf(reason);

    private static string GapText(TimelineGap gap)
    {
        var (start, end) = (gap.Start.ToLocalTime(), gap.End.ToLocalTime());
        var format = start.Date == end.Date ? "t" : "g";
        return string.Format(
            CultureInfo.CurrentCulture,
            Translations.GapSpan4,
            ReasonText(gap.Reason),
            start.ToString(format, CultureInfo.CurrentCulture),
            end.ToString(format, CultureInfo.CurrentCulture),
            FormatDuration(gap.End - gap.Start));
    }

    [RelayCommand]
    private void PlayAt(double fraction)
        => playAt(this, fraction);
}