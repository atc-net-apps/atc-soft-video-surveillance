namespace AtcSoft.VideoSurveillance.Wpf.Core.Helpers;

internal static class BoundingBoxExtensions
{
    public static Rect ToRect(this BoundingBox box)
        => new(box.X, box.Y, box.Width, box.Height);

    public static IReadOnlyList<Rect> ToRects(
        this IReadOnlyList<BoundingBox> boxes)
        => boxes.Select(b => b.ToRect()).ToList();
}