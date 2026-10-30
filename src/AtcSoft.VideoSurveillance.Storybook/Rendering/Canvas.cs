namespace AtcSoft.VideoSurveillance.Storybook.Rendering;

/// <summary>
/// A grey picture to draw shapes in. Every shape can record the box of the pixels it actually
/// set, clipped to the picture, which is the truth of an object.
/// </summary>
internal sealed class Canvas
{
    private double minX;
    private double minY;
    private double maxX;
    private double maxY;

    public Canvas(
        int width,
        int height)
    {
        Width = width;
        Height = height;
        Pixels = new int[width * height];
        ResetTrace();
    }

    public int Width { get; }

    public int Height { get; }

    // Wider than a byte, so brightening and noise can go out of range before the final clamp.
    public int[] Pixels { get; }

    public int TracedPixels { get; private set; }

    /// <summary>
    /// Starts recording the box of what is drawn next.
    /// </summary>
    public void ResetTrace()
    {
        minX = double.MaxValue;
        minY = double.MaxValue;
        maxX = double.MinValue;
        maxY = double.MinValue;
        TracedPixels = 0;
    }

    /// <summary>
    /// Gets the box of what was drawn since <see cref="ResetTrace"/>, or <see langword="null"/>.
    /// </summary>
    public PixelBox? TracedBox()
        => TracedPixels == 0 ? null : new PixelBox(minX, minY, maxX - minX + 1, maxY - minY + 1);

    public void Set(
        int x,
        int y,
        int value)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height)
        {
            return;
        }

        Pixels[(y * Width) + x] = value;
        TracedPixels++;
        minX = Math.Min(minX, x);
        minY = Math.Min(minY, y);
        maxX = Math.Max(maxX, x);
        maxY = Math.Max(maxY, y);
    }

    /// <summary>
    /// Fills a rectangle; <paramref name="shade"/> gives each pixel's value from its place.
    /// </summary>
    public void FillRect(
        double x,
        double y,
        double width,
        double height,
        Func<int, int, int> shade)
    {
        var x0 = (int)Math.Floor(x);
        var y0 = (int)Math.Floor(y);
        var x1 = (int)Math.Ceiling(x + width);
        var y1 = (int)Math.Ceiling(y + height);
        for (var py = Math.Max(0, y0); py < Math.Min(Height, y1); py++)
        {
            for (var px = Math.Max(0, x0); px < Math.Min(Width, x1); px++)
            {
                Set(px, py, shade(px, py));
            }
        }
    }

    /// <summary>
    /// Fills an ellipse; <paramref name="shade"/> gives each pixel's value from its place.
    /// </summary>
    public void FillEllipse(
        double centerX,
        double centerY,
        double radiusX,
        double radiusY,
        Func<int, int, int> shade)
    {
        if (radiusX <= 0 || radiusY <= 0)
        {
            return;
        }

        var x0 = (int)Math.Floor(centerX - radiusX);
        var x1 = (int)Math.Ceiling(centerX + radiusX);
        var y0 = (int)Math.Floor(centerY - radiusY);
        var y1 = (int)Math.Ceiling(centerY + radiusY);
        for (var py = Math.Max(0, y0); py <= Math.Min(Height - 1, y1); py++)
        {
            for (var px = Math.Max(0, x0); px <= Math.Min(Width - 1, x1); px++)
            {
                var dx = (px + 0.5 - centerX) / radiusX;
                var dy = (py + 0.5 - centerY) / radiusY;
                if ((dx * dx) + (dy * dy) <= 1)
                {
                    Set(px, py, shade(px, py));
                }
            }
        }
    }

    /// <summary>
    /// Adds a value to every pixel (a lighting change).
    /// </summary>
    public void AddAll(int delta)
    {
        for (var i = 0; i < Pixels.Length; i++)
        {
            Pixels[i] += delta;
        }
    }

    public byte[] ToBytes()
    {
        var bytes = new byte[Pixels.Length];
        for (var i = 0; i < Pixels.Length; i++)
        {
            bytes[i] = (byte)Math.Clamp(Pixels[i], 0, 255);
        }

        return bytes;
    }
}