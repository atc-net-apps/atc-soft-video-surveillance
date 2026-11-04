namespace AtcSoft.Video.Surveillance.Tools.DetectionReview;

/// <summary>
/// Drawing on BGRA pixels, and finding the boxes a person drew in red.
/// </summary>
internal static class Bgra
{
    // Smaller red marks than this are specks (a stray click, the picture's own red), not a box.
    private const int MinStrokePixels = 30;

    public static void DrawBox(
        byte[] pixels,
        int width,
        int height,
        ReviewBox box,
        (byte R, byte G, byte B) colour,
        int thickness)
    {
        for (var t = 0; t < thickness; t++)
        {
            var left = box.X - t;
            var top = box.Y - t;
            var right = box.Right - 1 + t;
            var bottom = box.Bottom - 1 + t;
            for (var x = left; x <= right; x++)
            {
                Set(pixels, width, height, x, top, colour);
                Set(pixels, width, height, x, bottom, colour);
            }

            for (var y = top; y <= bottom; y++)
            {
                Set(pixels, width, height, left, y, colour);
                Set(pixels, width, height, right, y, colour);
            }
        }
    }

    /// <summary>
    /// Finds the boxes drawn in red: the red pixels that are not red in the original picture,
    /// grouped into strokes, each stroke's extent a box. Strokes whose boxes overlap are one box
    /// (a rectangle drawn in more than one go).
    /// </summary>
    public static List<ReviewBox> FindRedBoxes(
        byte[] drawn,
        byte[] original,
        int width,
        int height)
    {
        var red = new bool[width * height];
        for (var i = 0; i < red.Length; i++)
        {
            red[i] = IsRed(drawn, i * 4) && !IsRed(original, i * 4);
        }

        var boxes = new List<ReviewBox>();
        var seen = new bool[red.Length];
        var queue = new Queue<int>();
        for (var start = 0; start < red.Length; start++)
        {
            if (!red[start] || seen[start])
            {
                continue;
            }

            var (minX, minY, maxX, maxY, count) = (int.MaxValue, int.MaxValue, -1, -1, 0);
            seen[start] = true;
            queue.Enqueue(start);
            while (queue.TryDequeue(out var i))
            {
                var x = i % width;
                var y = i / width;
                (minX, minY, maxX, maxY, count) = (Math.Min(minX, x), Math.Min(minY, y), Math.Max(maxX, x), Math.Max(maxY, y), count + 1);
                for (var dy = -1; dy <= 1; dy++)
                {
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        var nx = x + dx;
                        var ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= width || ny >= height)
                        {
                            continue;
                        }

                        var n = (ny * width) + nx;
                        if (red[n] && !seen[n])
                        {
                            seen[n] = true;
                            queue.Enqueue(n);
                        }
                    }
                }
            }

            if (count >= MinStrokePixels)
            {
                boxes.Add(new ReviewBox { X = minX, Y = minY, Width = maxX - minX + 1, Height = maxY - minY + 1 });
            }
        }

        return Merge(boxes);
    }

    private static List<ReviewBox> Merge(List<ReviewBox> boxes)
    {
        var merged = true;
        while (merged)
        {
            merged = false;
            for (var i = 0; i < boxes.Count && !merged; i++)
            {
                for (var j = i + 1; j < boxes.Count && !merged; j++)
                {
                    var a = boxes[i];
                    var b = boxes[j];
                    if (a.IntersectionOverUnion(b) > 0)
                    {
                        var x = Math.Min(a.X, b.X);
                        var y = Math.Min(a.Y, b.Y);
                        boxes[i] = new ReviewBox { X = x, Y = y, Width = Math.Max(a.Right, b.Right) - x, Height = Math.Max(a.Bottom, b.Bottom) - y };
                        boxes.RemoveAt(j);
                        merged = true;
                    }
                }
            }
        }

        return boxes;
    }

    // Red as a paint program draws it, its anti-aliased edge included; not yellow, orange or pink.
    private static bool IsRed(
        byte[] pixels,
        int i)
        => pixels[i + 2] >= 160 && pixels[i + 1] <= 90 && pixels[i] <= 90;

    private static void Set(
        byte[] pixels,
        int width,
        int height,
        int x,
        int y,
        (byte R, byte G, byte B) colour)
    {
        if (x < 0 || y < 0 || x >= width || y >= height)
        {
            return;
        }

        var i = ((y * width) + x) * 4;
        pixels[i] = colour.B;
        pixels[i + 1] = colour.G;
        pixels[i + 2] = colour.R;
        pixels[i + 3] = 255;
    }
}