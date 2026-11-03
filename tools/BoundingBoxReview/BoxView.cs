namespace BoundingBoxReview;

/// <summary>
/// A box as the picture shows it, in the video's pixels.
/// </summary>
public sealed record BoxView(
    double X,
    double Y,
    double Width,
    double Height)
{
    public static BoxView Of(ReviewBox box)
    {
        ArgumentNullException.ThrowIfNull(box);
        return new BoxView(box.X, box.Y, box.Width, box.Height);
    }

    public static BoxView Between(
        Point a,
        Point b)
        => new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
}