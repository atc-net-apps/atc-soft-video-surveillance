namespace AtcSoft.VideoSurveillance.Helpers;

/// <summary>
/// Computes capped exponential backoff delays for reconnect attempts to
/// dead/unavailable resources (RTSP cameras, GitHub API, etc.).
/// </summary>
/// <remarks>
/// A persistently dead camera with a fixed 30 s retry produces ~2,880
/// failed reconnect attempts per day and ~1 M per year. Capped exponential
/// backoff cuts that to a small constant rate while still recovering
/// promptly when the resource comes back.
/// </remarks>
public static class ReconnectBackoff
{
    /// <summary>
    /// Default base delay for the first failed attempt (30 s).
    /// </summary>
    public static readonly TimeSpan DefaultBaseDelay = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Default ceiling for the backoff (15 minutes).
    /// </summary>
    public static readonly TimeSpan DefaultMaxDelay = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Returns the delay before the <paramref name="consecutiveFailures"/>
    /// reconnect attempt, doubling on each failure and capped at
    /// <paramref name="maxDelay"/>.
    /// </summary>
    /// <param name="consecutiveFailures">
    /// Number of consecutive failures so far. <c>0</c> returns
    /// <see cref="TimeSpan.Zero"/> (no delay before first attempt).
    /// </param>
    /// <param name="baseDelay">
    /// Delay used after the first failure. Defaults to
    /// <see cref="DefaultBaseDelay"/>.
    /// </param>
    /// <param name="maxDelay">
    /// Ceiling for the returned delay. Defaults to
    /// <see cref="DefaultMaxDelay"/>.
    /// </param>
    public static TimeSpan ComputeDelay(
        int consecutiveFailures,
        TimeSpan? baseDelay = null,
        TimeSpan? maxDelay = null)
    {
        if (consecutiveFailures <= 0)
        {
            return TimeSpan.Zero;
        }

        var b = baseDelay ?? DefaultBaseDelay;
        var m = maxDelay ?? DefaultMaxDelay;

        // Clamp shift to avoid overflow on absurd failure counts; caps
        // out long before reaching the maxDelay anyway.
        var shift = Math.Min(consecutiveFailures - 1, 30);
        var ticks = b.Ticks * (1L << shift);

        if (ticks < 0 || ticks > m.Ticks)
        {
            return m;
        }

        return TimeSpan.FromTicks(ticks);
    }
}