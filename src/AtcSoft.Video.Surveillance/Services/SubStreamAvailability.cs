namespace AtcSoft.Video.Surveillance.Services;

/// <summary>
/// Which cameras' sub-streams would not open lately. Such a camera is opened on its main stream
/// alone for a while (<see cref="RetryAfter"/>), then its sub-stream is tried again: a wrong path
/// costs the CPU saving, never the recording.
/// </summary>
public sealed class SubStreamAvailability
{
    /// <summary>
    /// How long a camera whose sub-stream failed is opened without it.
    /// </summary>
    public static readonly TimeSpan RetryAfter = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<Guid, Failure> failures = new();
    private readonly TimeProvider time;

    public SubStreamAvailability(TimeProvider? time = null)
        => this.time = time ?? TimeProvider.System;

    /// <summary>
    /// Notes that a camera's sub-stream would not open (or stopped).
    /// </summary>
    /// <param name="cameraId">The camera.</param>
    /// <param name="subStreamPath">The sub-stream it failed on: another path is tried at once.</param>
    /// <param name="reason">What went wrong, for the log and the camera's status.</param>
    public void Failed(
        Guid cameraId,
        string? subStreamPath,
        string reason)
        => failures[cameraId] = new Failure(subStreamPath, reason, time.GetUtcNow());

    /// <summary>
    /// Forgets a camera's failure: its sub-stream played.
    /// </summary>
    /// <param name="cameraId">The camera.</param>
    public void Played(Guid cameraId)
        => failures.TryRemove(cameraId, out _);

    /// <summary>
    /// Tells whether a camera's sub-stream is to be opened.
    /// </summary>
    /// <param name="camera">The camera, as it is now.</param>
    /// <returns><see langword="true"/> when it has one that has not failed lately.</returns>
    public bool IsUsable(CameraConfiguration camera)
    {
        ArgumentNullException.ThrowIfNull(camera);
        return camera.Connection.HasSubStream && !IsFailing(camera);
    }

    /// <summary>
    /// Tells whether a camera's sub-stream failed lately (and was not changed since).
    /// </summary>
    /// <param name="camera">The camera, as it is now.</param>
    /// <returns><see langword="true"/> while it is opened without its sub-stream.</returns>
    public bool IsFailing(CameraConfiguration camera)
    {
        ArgumentNullException.ThrowIfNull(camera);
        return failures.TryGetValue(camera.Id, out var failure) &&
               string.Equals(failure.SubStreamPath, camera.Connection.SubStreamPath, StringComparison.Ordinal) &&
               time.GetUtcNow() - failure.At < RetryAfter;
    }

    private sealed record Failure(
        string? SubStreamPath,
        string Reason,
        DateTimeOffset At);
}