namespace AtcSoft.VideoSurveillance.Services;

/// <summary>
/// Service for generating recording thumbnails.
/// </summary>
public interface IThumbnailGeneratorService
{
    /// <summary>
    /// Starts capturing frames for the thumbnail of a recording.
    /// </summary>
    /// <param name="cameraId">The camera being recorded.</param>
    /// <param name="cameraName">The camera's display name, for logging.</param>
    /// <param name="pipeline">The pipeline to capture frames from.</param>
    /// <param name="videoFilePath">The recording the thumbnail belongs to.</param>
    /// <param name="tileCount">The number of frames in the thumbnail: 1 or 4.</param>
    /// <param name="thumbnailFilePath">
    /// Where to write the thumbnail. When <see langword="null"/> it is written
    /// next to the recording with a <c>.png</c> extension.
    /// </param>
    void StartCapture(
        Guid cameraId,
        string cameraName,
        IMediaPipeline pipeline,
        string videoFilePath,
        int tileCount = 4,
        string? thumbnailFilePath = null);

    void StopCapture(Guid cameraId);

    bool IsCaptureActive(Guid cameraId);

    void StopAllCaptures();
}