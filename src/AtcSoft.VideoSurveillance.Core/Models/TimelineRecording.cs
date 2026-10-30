namespace AtcSoft.VideoSurveillance.Models;

/// <summary>
/// One recording of a camera as a timeline sees it. Where it covers comes from the video file
/// (its name's time and when it was last written), not from its index, so a recording that was
/// not analysed still counts as recorded.
/// </summary>
/// <param name="VideoPath">The video, as the caller names recordings (a path below the recordings root).</param>
/// <param name="Start">When the recording starts, in UTC.</param>
/// <param name="End">When it ends (now for one in progress), in UTC.</param>
/// <param name="Activity">Its activity, or <see langword="null"/> when it has no index.</param>
public sealed record TimelineRecording(
    string VideoPath,
    DateTimeOffset Start,
    DateTimeOffset End,
    ActivitySummary? Activity);