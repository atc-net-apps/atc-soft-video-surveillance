namespace AtcSoft.VideoSurveillance.Events;

/// <summary>
/// Activity on a recorded camera started (a confirmed track appeared) or ended (nothing moved
/// for <c>ActivitySummarizer.MergeGapSeconds</c>): what an open timeline needs to grow live.
/// </summary>
/// <param name="cameraId">The camera.</param>
/// <param name="isActive">Whether it started; ended when <see langword="false"/>.</param>
/// <param name="start">When it started, in UTC.</param>
/// <param name="end">When it ended, in UTC; <see langword="null"/> while it goes on.</param>
/// <param name="videoPath">The recording it started in.</param>
/// <param name="offsetSeconds">Where in that recording it started, in seconds.</param>
/// <param name="peakLevel">The highest level so far, 0 to 100.</param>
/// <param name="tracks">The most moving things at once so far.</param>
public sealed class CameraActivityChangedEventArgs(
    Guid cameraId,
    bool isActive,
    DateTimeOffset start,
    DateTimeOffset? end,
    string videoPath,
    double offsetSeconds,
    double peakLevel,
    int tracks) : EventArgs
{
    public Guid CameraId { get; } = cameraId;

    public bool IsActive { get; } = isActive;

    public DateTimeOffset Start { get; } = start;

    public DateTimeOffset? End { get; } = end;

    public string VideoPath { get; } = videoPath;

    public double OffsetSeconds { get; } = offsetSeconds;

    public double PeakLevel { get; } = peakLevel;

    public int Tracks { get; } = tracks;
}