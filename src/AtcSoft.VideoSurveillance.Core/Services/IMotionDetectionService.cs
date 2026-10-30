namespace AtcSoft.VideoSurveillance.Services;

/// <summary>
/// Service for detecting motion in camera video streams.
/// </summary>
public interface IMotionDetectionService
{
    event EventHandler<MotionDetectedEventArgs>? MotionDetected;

    /// <summary>
    /// Raised for every analysed picture of every camera, watched for motion or analysed only
    /// for the activity index.
    /// </summary>
    event EventHandler<PictureAnalyzedEventArgs>? PictureAnalyzed;

    void StartDetection(
        Guid cameraId,
        IMediaPipeline pipeline,
        MotionDetectionSettings? settings = null,
        IReadOnlyList<MotionZone>? zones = null);

    void StopDetection(Guid cameraId);

    bool IsDetectionActive(Guid cameraId);

    /// <summary>
    /// Analyses a camera for its activity index. A camera already analysed goes on as it is;
    /// one that is not starts without reporting motion: no <see cref="MotionDetected"/>, so no
    /// badge, webhook or motion recording comes of it. <see cref="StopDetection"/> on a camera
    /// that is indexed only stops the motion reports.
    /// </summary>
    /// <param name="cameraId">The camera.</param>
    /// <param name="pipeline">The camera's pipeline, to take pictures from.</param>
    /// <param name="settings">The camera's motion settings.</param>
    /// <param name="zones">The camera's motion zones.</param>
    void StartIndexing(
        Guid cameraId,
        IMediaPipeline pipeline,
        MotionDetectionSettings? settings = null,
        IReadOnlyList<MotionZone>? zones = null);

    /// <summary>
    /// Gives a camera that is analysed its zones as they are now, edited or turned with the
    /// picture, without starting the analysis over. A camera that is not analysed takes its zones
    /// when it starts.
    /// </summary>
    /// <param name="cameraId">The camera.</param>
    /// <param name="zones">The camera's motion zones.</param>
    void UseZones(
        Guid cameraId,
        IReadOnlyList<MotionZone> zones);

    /// <summary>
    /// Stops analysing a camera for its activity index. A camera watched for motion goes on.
    /// </summary>
    /// <param name="cameraId">The camera.</param>
    void StopIndexing(Guid cameraId);

    /// <summary>
    /// Determines whether a camera is analysed for its activity index.
    /// </summary>
    /// <param name="cameraId">The camera.</param>
    bool IsIndexing(Guid cameraId);

    bool IsMotionDetected(Guid cameraId);

    IReadOnlyList<BoundingBox> GetLastBoundingBoxes(Guid cameraId);

    (int Width, int Height) GetAnalysisResolution(Guid cameraId);
}