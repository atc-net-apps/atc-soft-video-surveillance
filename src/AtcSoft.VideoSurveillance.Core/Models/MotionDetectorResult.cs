namespace AtcSoft.VideoSurveillance.Models;

/// <summary>
/// What a motion detector found in one picture.
/// </summary>
/// <param name="IsActive">Whether motion is on after this picture (the post-motion hold included).</param>
/// <param name="Change">What this picture changed: motion started, goes on with new boxes, or ended.</param>
/// <param name="ChangePercent">The share of the picture that changed, 0 to 100.</param>
/// <param name="Boxes">The boxes to show, in analysis coordinates; empty unless motion started or goes on.</param>
public sealed record MotionDetectorResult(
    bool IsActive,
    MotionHoldChange Change,
    double ChangePercent,
    IReadOnlyList<BoundingBox> Boxes)
{
    /// <summary>
    /// Gets the result when nothing was found and nothing changed.
    /// </summary>
    public static MotionDetectorResult Quiet { get; } = new(false, MotionHoldChange.None, 0, []);
}