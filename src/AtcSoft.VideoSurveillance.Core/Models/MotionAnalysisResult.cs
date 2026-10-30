namespace AtcSoft.VideoSurveillance.Models;

/// <summary>
/// What comparing two pictures found.
/// </summary>
/// <param name="ChangePercent">The share of the picture that changed, 0 to 100.</param>
/// <param name="BoundingBoxes">Where it changed, in the coordinates of the analysed picture, largest first.</param>
public sealed record MotionAnalysisResult(
    double ChangePercent,
    IReadOnlyList<BoundingBox> BoundingBoxes)
{
    /// <summary>
    /// Gets the result for pictures in which nothing changed.
    /// </summary>
    public static MotionAnalysisResult None { get; } = new(0, []);
}