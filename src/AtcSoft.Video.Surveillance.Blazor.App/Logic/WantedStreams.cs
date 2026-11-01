namespace AtcSoft.Video.Surveillance.Blazor.App.Logic;

/// <summary>
/// The streams the viewer asked the live view to play. A stream that broke
/// (the camera disconnected, the server restarted, the start failed) stays
/// wanted and is started again, a little later each time it fails, until the
/// viewer stops it or leaves its layout.
/// </summary>
public sealed class WantedStreams
{
    // The waits before each new start of a stream that failed, the last one repeated.
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(20),
        TimeSpan.FromSeconds(40),
        TimeSpan.FromSeconds(60),
    ];

    private readonly Dictionary<Guid, int> failures = new();

    /// <summary>
    /// Gets the cameras whose streams are wanted.
    /// </summary>
    public IReadOnlyCollection<Guid> Cameras
        => failures.Keys;

    /// <summary>
    /// The viewer asked for the stream.
    /// </summary>
    public void Want(Guid cameraId)
        => failures.TryAdd(cameraId, 0);

    /// <summary>
    /// The viewer stopped the stream, or left its layout.
    /// </summary>
    public void Forget(Guid cameraId)
        => failures.Remove(cameraId);

    public bool IsWanted(Guid cameraId)
        => failures.ContainsKey(cameraId);

    /// <summary>
    /// The stream plays: the next failure waits the shortest time again.
    /// </summary>
    public void Started(Guid cameraId)
    {
        if (failures.ContainsKey(cameraId))
        {
            failures[cameraId] = 0;
        }
    }

    /// <summary>
    /// The stream broke or could not start. Returns how long to wait before it
    /// is started again, or <see langword="null"/> when it is not wanted.
    /// </summary>
    public TimeSpan? Failed(Guid cameraId)
    {
        if (!failures.TryGetValue(cameraId, out var count))
        {
            return null;
        }

        failures[cameraId] = count + 1;
        return DelayFor(count);
    }

    /// <summary>
    /// Gets the wait after <paramref name="failures"/> failures in a row: 5, 10,
    /// 20 and 40 seconds, then a minute.
    /// </summary>
    public static TimeSpan DelayFor(int failures)
        => RetryDelays[Math.Clamp(failures, 0, RetryDelays.Length - 1)];
}