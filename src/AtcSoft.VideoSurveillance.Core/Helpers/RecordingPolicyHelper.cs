namespace AtcSoft.VideoSurveillance.Helpers;

/// <summary>
/// Shared helper for resolving recording policy decisions that consider per-camera overrides.
/// </summary>
public static class RecordingPolicyHelper
{
    /// <summary>
    /// Determines whether a camera should record on connect, considering per-camera overrides.
    /// </summary>
    /// <param name="camera">The camera configuration (may have per-camera overrides).</param>
    /// <param name="appDefault">The application-level default for EnableRecordingOnConnect.</param>
    /// <returns><c>true</c> if recording should start when the camera connects.</returns>
    public static bool ShouldRecordOnConnect(
        CameraConfiguration camera,
        bool appDefault)
    {
        ArgumentNullException.ThrowIfNull(camera);
        return camera.Overrides?.Recording.EnableRecordingOnConnect ?? appDefault;
    }

    /// <summary>
    /// Resolves the video transcode codec for a camera, considering per-camera overrides.
    /// </summary>
    /// <param name="camera">The camera configuration (may have per-camera overrides).</param>
    /// <param name="appDefault">The application-level default for TranscodeVideoCodec.</param>
    /// <returns>The resolved <see cref="VideoTranscodeCodec"/> for recording.</returns>
    public static VideoTranscodeCodec ResolveTranscodeCodec(
        CameraConfiguration camera,
        VideoTranscodeCodec appDefault)
    {
        ArgumentNullException.ThrowIfNull(camera);
        return camera.Overrides?.Recording.TranscodeVideoCodec ?? appDefault;
    }
}