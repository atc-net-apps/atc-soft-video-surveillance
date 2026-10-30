namespace AtcSoft.VideoSurveillance.Api.Services;

/// <summary>
/// Per-session diagnostic projection returned by
/// <see cref="ServerRecordingService.GetDiagnostics"/>. Used by the
/// /health/recordings endpoint to give operators live visibility into
/// active recordings during long-running deployments.
/// </summary>
/// <param name="CameraId">Camera identifier.</param>
/// <param name="CameraName">Camera display name (already on the session — copied so callers don't need to look it up).</param>
/// <param name="FilePath">Absolute path of the file currently being written.</param>
/// <param name="StartedAtUtc">UTC start timestamp of the current segment.</param>
/// <param name="Duration">Wall-clock duration since the current segment started.</param>
/// <param name="IsPipelineActive">
/// True if the underlying pipeline is currently producing packets. False
/// means the recording is "stuck" — session present but pipeline died;
/// the reaper will sweep on the next tick.
/// </param>
public sealed record RecordingDiagnostics(
    Guid CameraId,
    string CameraName,
    string FilePath,
    DateTime StartedAtUtc,
    TimeSpan Duration,
    bool IsPipelineActive)
{
    /// <summary>
    /// True when the recording drive's free space is below the configured
    /// <c>MinFreeSpaceMb</c> threshold at the time diagnostics were collected.
    /// </summary>
    public bool DiskSpaceLow { get; init; }
}