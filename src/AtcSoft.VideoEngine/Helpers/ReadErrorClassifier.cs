namespace AtcSoft.VideoEngine.Helpers;

/// <summary>
/// Pure classification of read-loop error bursts. Lives outside
/// <see cref="VideoPlayer"/> so the heuristic can be unit-tested without
/// the FFmpeg-bound demuxer/decoder dependency.
/// </summary>
public static class ReadErrorClassifier
{
    /// <summary>
    /// Decide how to label a burst of consecutive <c>av_read_frame</c>
    /// failures that exceeded the read-loop's tolerance threshold.
    /// </summary>
    /// <param name="inputFormat">The input format the player opened
    /// with. Only <see cref="InputFormatKind.Dshow"/> currently maps to
    /// the device-busy heuristic — other local-device kinds
    /// (V4l2, AVFoundation) reserved for the deferred platform phases.</param>
    /// <param name="anyFramesReceived"><see langword="true"/> if the
    /// read loop produced at least one successful packet before the
    /// error burst. A camera that streamed then stopped is treated as
    /// <see cref="StreamFailureReason.Unknown"/> (likely a hardware
    /// disconnect / driver hiccup, not exclusive lock).</param>
    /// <param name="lastErrorCode">Most recent FFmpeg return code from
    /// <c>av_read_frame</c>. Logged for diagnostic purposes (see
    /// <c>VideoPlayer.Log.LogReadErrorCode</c>) but no longer used to
    /// gate the classification — see comment below.</param>
    public static StreamFailureReason Classify(
        InputFormatKind inputFormat,
        bool anyFramesReceived,
        int lastErrorCode)
    {
        _ = lastErrorCode;

        if (inputFormat != InputFormatKind.Dshow)
        {
            return StreamFailureReason.Unknown;
        }

        if (anyFramesReceived)
        {
            return StreamFailureReason.Unknown;
        }

        // A dshow source that opened successfully but never delivered a
        // single frame, then tripped the read-error threshold, is almost
        // always "device blocked by another process" — Teams / browser /
        // OBS holding the exclusive capture lock. Empirically the
        // underlying FFmpeg error varies: -11 (AVERROR(EAGAIN)) on some
        // builds, -5 (AVERROR(EIO)) on others (observed against
        // "USB2.0 FHD UVC WebCam" with Teams active, see commit log).
        // Matching specific codes invited false negatives, and the
        // "dshow + zero frames received before burst" predicate is
        // already a strong enough discriminator: network cameras are
        // excluded by inputFormat, and a camera that streamed then
        // stopped mid-session is excluded by anyFramesReceived. False
        // positives (driver crash, device powered off pre-open) still
        // give the operator an actionable starting hint.
        return StreamFailureReason.DeviceBusy;
    }
}