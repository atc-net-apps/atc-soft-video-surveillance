namespace AtcSoft.VideoSurveillance.Helpers.MotionDetection;

/// <summary>
/// A camera's zones filled in at the analysis size: the pixels motion detection ignores, and the
/// watch zone each pixel is in. A pixel is in a polygon when its centre is (even-odd rule), so a
/// concave shape keeps its notch; two ignore zones that overlap are both ignored, and of two watch
/// zones that overlap the one drawn last, on top, counts. An ignore zone wins over a watch zone.
/// </summary>
public static class ZoneMask
{
    /// <summary>
    /// Fills in the ignore zones.
    /// </summary>
    /// <param name="zones">The camera's zones; those that are not ignore zones, or have fewer than three corners, are left out.</param>
    /// <param name="width">The picture's width.</param>
    /// <param name="height">The picture's height.</param>
    /// <returns>One byte per pixel, 1 where ignored; <see langword="null"/> when nothing is.</returns>
    public static byte[]? Ignored(
        IReadOnlyList<MotionZone>? zones,
        int width,
        int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        var ignore = zones?.Where(z => z.Kind == MotionZoneKind.Ignore && z.Points.Count >= 3).ToList() ?? [];
        if (ignore.Count == 0)
        {
            return null;
        }

        var mask = new byte[width * height];
        foreach (var zone in ignore)
        {
            Fill(zone, width, height, mask, 1);
        }

        return mask;
    }

    /// <summary>
    /// Gets the watch zones, in the order they are drawn: what the numbers of <see cref="Watched"/> stand for.
    /// </summary>
    /// <param name="zones">The camera's zones.</param>
    public static IReadOnlyList<MotionZone> WatchZones(
        IReadOnlyList<MotionZone>? zones)
        => zones?.Where(z => z.Kind == MotionZoneKind.Watch && z.Points.Count >= 3).Take(MotionZoneEditing.MaxZones).ToList() ?? [];

    /// <summary>
    /// Fills in the watch zones.
    /// </summary>
    /// <param name="zones">The camera's zones; those that are not watch zones, or have fewer than three corners, are left out.</param>
    /// <param name="width">The picture's width.</param>
    /// <param name="height">The picture's height.</param>
    /// <returns>
    /// One byte per pixel: 0 outside every watch zone, else 1 + the zone's place in <see cref="WatchZones"/>,
    /// the topmost where they overlap; <see langword="null"/> when there are none.
    /// </returns>
    public static byte[]? Watched(
        IReadOnlyList<MotionZone>? zones,
        int width,
        int height)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);

        var watch = WatchZones(zones);
        if (watch.Count == 0)
        {
            return null;
        }

        var map = new byte[width * height];
        for (var i = 0; i < watch.Count; i++)
        {
            Fill(watch[i], width, height, map, (byte)(i + 1));
        }

        return map;
    }

    private static void Fill(
        MotionZone zone,
        int width,
        int height,
        byte[] mask,
        byte value)
    {
        var crossings = new List<double>();
        var points = zone.Points.Select(p => (X: p.X * width, Y: p.Y * height)).ToList();
        for (var y = 0; y < height; y++)
        {
            // Where the row through the pixel centres crosses the edges.
            var row = y + 0.5;
            crossings.Clear();
            for (var i = 0; i < points.Count; i++)
            {
                var a = points[i];
                var b = points[(i + 1) % points.Count];
                if ((a.Y <= row && b.Y > row) || (b.Y <= row && a.Y > row))
                {
                    crossings.Add(a.X + ((row - a.Y) / (b.Y - a.Y) * (b.X - a.X)));
                }
            }

            crossings.Sort();
            for (var c = 0; c + 1 < crossings.Count; c += 2)
            {
                // Pixels whose centre lies between the two crossings.
                var from = Math.Max(0, (int)Math.Ceiling(crossings[c] - 0.5));
                var to = Math.Min(width - 1, (int)Math.Floor(crossings[c + 1] - 0.5));
                for (var x = from; x <= to; x++)
                {
                    mask[(y * width) + x] = value;
                }
            }
        }
    }
}