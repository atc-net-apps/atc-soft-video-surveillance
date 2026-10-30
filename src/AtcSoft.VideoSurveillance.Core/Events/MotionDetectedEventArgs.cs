namespace AtcSoft.VideoSurveillance.Events;

/// <summary>
/// Event arguments for motion detection events.
/// Uses <see cref="BoundingBox"/> instead of WPF-specific System.Windows.Rect.
/// </summary>
public class MotionDetectedEventArgs : EventArgs
{
    public MotionDetectedEventArgs(
        Guid cameraId,
        double changePercentage,
        bool isMotionActive,
        IReadOnlyList<BoundingBox>? boundingBoxes = null,
        int analysisWidth = 320,
        int analysisHeight = 240)
    {
        CameraId = cameraId;
        ChangePercentage = changePercentage;
        IsMotionActive = isMotionActive;
        BoundingBoxes = boundingBoxes ?? [];
        AnalysisWidth = analysisWidth;
        AnalysisHeight = analysisHeight;
        Timestamp = DateTime.UtcNow;
    }

    public Guid CameraId { get; }

    public double ChangePercentage { get; }

    public bool IsMotionActive { get; }

    public IReadOnlyList<BoundingBox> BoundingBoxes { get; }

    public bool HasBoundingBoxes => BoundingBoxes.Count > 0;

    public int AnalysisWidth { get; }

    public int AnalysisHeight { get; }

    public DateTime Timestamp { get; }

    /// <inheritdoc />
    public override string ToString()
        => $"MotionDetected {{ CameraId={CameraId.ToString().Substring(0, 8)}, Active={IsMotionActive}, Change={ChangePercentage.ToString("F2", CultureInfo.InvariantCulture)}% }}";
}