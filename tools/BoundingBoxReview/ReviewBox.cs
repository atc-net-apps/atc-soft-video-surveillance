namespace BoundingBoxReview;

/// <summary>
/// A box in the video's pixels, as the video is displayed (turned as the camera is).
/// </summary>
public sealed class ReviewBox
{
    public int X { get; set; }

    public int Y { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Track { get; set; }

    [JsonIgnore]
    public int Right
        => X + Width;

    [JsonIgnore]
    public int Bottom
        => Y + Height;

    [JsonIgnore]
    public long Area
        => (long)Width * Height;

    public static ReviewBox From(
        PixelBox box,
        int? track,
        int width,
        int height)
        => Within(box.X, box.Y, box.Width, box.Height, width, height, track);

    public static ReviewBox Within(
        double x,
        double y,
        double boxWidth,
        double boxHeight,
        int width,
        int height,
        int? track = null)
    {
        var left = Math.Clamp((int)Math.Round(x), 0, width - 1);
        var top = Math.Clamp((int)Math.Round(y), 0, height - 1);
        return new ReviewBox
        {
            X = left,
            Y = top,
            Width = Math.Clamp((int)Math.Round(boxWidth), 1, width - left),
            Height = Math.Clamp((int)Math.Round(boxHeight), 1, height - top),
            Track = track,
        };
    }

    public bool SameBox(ReviewBox other)
        => other is not null && X == other.X && Y == other.Y && Width == other.Width && Height == other.Height;

    public ReviewBox Copy()
        => new() { X = X, Y = Y, Width = Width, Height = Height };

    public double IntersectionOverUnion(ReviewBox other)
    {
        ArgumentNullException.ThrowIfNull(other);

        var w = Math.Min(Right, other.Right) - Math.Max(X, other.X);
        var h = Math.Min(Bottom, other.Bottom) - Math.Max(Y, other.Y);
        if (w <= 0 || h <= 0)
        {
            return 0;
        }

        var shared = (double)w * h;
        return shared / (Area + other.Area - shared);
    }

    public override string ToString()
        => string.Create(CultureInfo.InvariantCulture, $"{X},{Y} {Width}×{Height}");
}