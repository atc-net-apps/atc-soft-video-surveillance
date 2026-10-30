namespace AtcSoft.VideoSurveillance.Storybook.Truth;

/// <summary>
/// A box in the pixels of a frame.
/// </summary>
/// <param name="X">The left edge.</param>
/// <param name="Y">The top edge.</param>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct PixelBox(
    double X,
    double Y,
    double Width,
    double Height)
{
    public double Area
        => Math.Max(0, Width) * Math.Max(0, Height);

    public double CenterX
        => X + (Width / 2);

    public double CenterY
        => Y + (Height / 2);

    /// <summary>
    /// Gets the overlap with another box: the shared area over the area of both.
    /// </summary>
    /// <param name="other">The other box.</param>
    /// <returns>0 to 1.</returns>
    public double IntersectionOverUnion(PixelBox other)
    {
        var width = Math.Min(X + Width, other.X + other.Width) - Math.Max(X, other.X);
        var height = Math.Min(Y + Height, other.Y + other.Height) - Math.Max(Y, other.Y);
        if (width <= 0 || height <= 0)
        {
            return 0;
        }

        var shared = width * height;
        return shared / (Area + other.Area - shared);
    }

    /// <summary>
    /// Determines whether a point lies inside the box.
    /// </summary>
    /// <param name="x">The point's x.</param>
    /// <param name="y">The point's y.</param>
    public bool Contains(
        double x,
        double y)
        => x >= X && x <= X + Width && y >= Y && y <= Y + Height;
}