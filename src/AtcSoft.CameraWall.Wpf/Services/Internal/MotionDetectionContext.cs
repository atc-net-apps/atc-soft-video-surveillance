// ReSharper disable InconsistentNaming
namespace AtcSoft.CameraWall.Wpf.Services.Internal;

/// <summary>
/// Internal context for tracking motion detection state per camera.
/// </summary>
internal sealed class MotionDetectionContext : IDisposable
{
    public MotionDetectionContext(
        Guid cameraId,
        IMediaPipeline pipeline,
        MotionDetectionSettings settings,
        IReadOnlyList<MotionZone>? zones = null)
    {
        CameraId = cameraId;
        Pipeline = pipeline;
        Settings = settings;
        Detector = new BuiltInMotionDetector(settings, zones: zones);
    }

    public Guid CameraId { get; }

    public IMediaPipeline Pipeline { get; }

    public MotionDetectionSettings Settings { get; }

    // The same detector as the server's, behind the shared seam.
    public IMotionDetector Detector { get; }

    // Whether motion is reported (the tile watches for it), and whether the pictures go to the
    // activity index (the camera records). At least one of them, or the camera is not analysed.
    public bool ReportsMotion { get; set; }

    public bool Indexes { get; set; }

    public bool IsMotionDetected { get; set; }

    public DateTime? LastMotionTime { get; set; }

    public IReadOnlyList<BoundingBox> LastBoundingBoxes { get; set; } = [];

    [SuppressMessage("", "SA1401: Volatile requires a field, not a property", Justification = "OK")]
    public volatile bool IsAnalyzing;

    public int ConsecutiveFails { get; set; }

    public DateTime LastAnalysisStartedUtc { get; set; }

    public void Dispose()
    {
        Detector.Reset();
        LastBoundingBoxes = [];
        ConsecutiveFails = 0;
    }
}