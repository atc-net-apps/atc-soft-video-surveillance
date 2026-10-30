namespace AtcSoft.VideoSurveillance.Events;

/// <summary>
/// Reasons for recording segmentation.
/// </summary>
public enum SegmentationReason
{
    /// <summary>
    /// Segmented at a clock-aligned interval boundary.
    /// </summary>
    IntervalBoundary,

    /// <summary>
    /// Segmented because the maximum duration was reached.
    /// </summary>
    MaxDurationReached,
}