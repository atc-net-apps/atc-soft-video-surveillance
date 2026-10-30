namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// Whether the web player loads a recording's activity again once the video tells its length: a
/// recording opened without one (from the Timeline page, or moved on to) was loaded in the fewest
/// stretches, too few for a minute zoomed in.
/// </summary>
public static class ActivityReload
{
    /// <summary>
    /// Gets whether a recording of this length should have more stretches than were loaded.
    /// </summary>
    /// <param name="loaded">How many stretches were loaded.</param>
    /// <param name="durationSeconds">How long the video says the recording is; 0 while not known.</param>
    public static bool IsNeeded(
        int loaded,
        double durationSeconds)
        => durationSeconds > 0 && ActivityTimelineZoom.BucketsFor(durationSeconds) > loaded;
}