namespace AtcSoft.Video.Surveillance.Blazor.App.Logic;

/// <summary>
/// The server sends a motion event for every picture it analyses; a view
/// shows at most five a second per camera. The end of motion always passes,
/// so the boxes never stay on a still picture.
/// </summary>
public sealed class MotionThrottle(TimeProvider? time = null)
{
    private readonly TimeProvider time = time ?? TimeProvider.System;
    private readonly Dictionary<Guid, DateTimeOffset> lastShown = new();

    /// <summary>
    /// Gets the shortest time between two motion events shown for a camera.
    /// </summary>
    public static TimeSpan Interval { get; } = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Gets a value indicating whether the event is shown; it is then counted.
    /// </summary>
    public bool ShouldShow(
        Guid cameraId,
        bool isMotionActive)
    {
        lock (lastShown)
        {
            var now = time.GetUtcNow();
            if (!isMotionActive)
            {
                lastShown.Remove(cameraId);
                return true;
            }

            if (lastShown.TryGetValue(cameraId, out var last) && now - last < Interval)
            {
                return false;
            }

            lastShown[cameraId] = now;
            return true;
        }
    }
}