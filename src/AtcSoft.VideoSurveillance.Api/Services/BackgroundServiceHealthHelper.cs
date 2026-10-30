namespace AtcSoft.VideoSurveillance.Api.Services;

/// <summary>
/// Shared helper for deriving a sensible max-staleness window from a
/// <see cref="BackgroundServiceBase{T}"/>'s repeat interval. The health service
/// flags a service as not-running once the gap between work ticks exceeds this
/// window, so it must allow for one full interval plus the work itself.
/// </summary>
internal static class BackgroundServiceHealthHelper
{
    // One full interval of slack on top of the configured interval absorbs a
    // slow tick (e.g. cleanup scanning a large tree) without false alarms,
    // while still catching a wedged DoWorkAsync within ~2 intervals.
    private const int BufferSeconds = 60;

    public static ushort StalenessFor(IBackgroundServiceOptions options)
    {
        var staleness = options.RepeatIntervalSeconds + (long)options.RepeatIntervalSeconds + BufferSeconds;
        return (ushort)Math.Min(staleness, ushort.MaxValue);
    }
}