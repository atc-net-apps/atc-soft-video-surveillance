namespace AtcSoft.VideoEngine;

/// <summary>
/// Classified cause of a transition into <see cref="PlayerState.Error"/>.
/// Surfaced via <see cref="PlayerStateChangedEventArgs.Reason"/> so the
/// UI can show a distinct hint (e.g. amber "device in use by another
/// app" row) instead of the generic <c>ConnectionFailed</c> indicator.
/// Best-effort classification — the underlying FFmpeg error codes vary
/// by version and platform; <see cref="Unknown"/> is the safe default.
/// </summary>
public enum StreamFailureReason
{
    /// <summary>
    /// No classification available, or the failure cause doesn't match
    /// any of the recognised patterns. Treat as a plain connection error.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// All reads after open returned <c>AVERROR(EAGAIN)</c> on a local
    /// device input (typically Windows DirectShow). Strong signal that
    /// another process holds the device's exclusive capture lock — Teams,
    /// a browser camera test, OBS, etc. Auto-reconnect should keep
    /// retrying so the tile resumes the moment the device is released.
    /// </summary>
    DeviceBusy = 1,

    /// <summary>
    /// The stream reached <c>AVERROR_EOF</c>. For RTSP cameras this
    /// usually means the upstream encoder restarted; for files it's the
    /// natural end of playback.
    /// </summary>
    EndOfStream = 2,
}