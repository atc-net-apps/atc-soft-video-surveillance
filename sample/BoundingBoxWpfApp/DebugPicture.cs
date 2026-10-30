namespace BoundingBoxWpfApp;

/// <summary>
/// Draws the picture the detector was given with the debug layers on it: the foreground in cyan
/// and the ignore zones in purple, each half over the grey.
/// </summary>
public static class DebugPicture
{
    /// <summary>
    /// Fills a BGRA picture.
    /// </summary>
    /// <param name="grey">The grey picture.</param>
    /// <param name="diagnostics">What the detector saw, or <see langword="null"/>.</param>
    /// <param name="foreground">Whether to show the foreground.</param>
    /// <param name="ignored">Whether to show the ignore zones.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="bgra">The picture to fill, four bytes a pixel.</param>
    public static void Compose(
        byte[] grey,
        MotionDiagnostics? diagnostics,
        bool foreground,
        bool ignored,
        int width,
        int height,
        byte[] bgra)
    {
        ArgumentNullException.ThrowIfNull(grey);
        ArgumentNullException.ThrowIfNull(bgra);

        var fits = diagnostics is not null && diagnostics.Width == width && diagnostics.Height == height;
        var foregroundOf = foreground && fits ? diagnostics : null;
        var ignoredOf = ignored && fits && diagnostics is { HasIgnored: true } ? diagnostics : null;
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var i = (y * width) + x;
                int b = grey[i], g = grey[i], r = grey[i];
                if (ignoredOf is not null && ignoredOf.IsIgnored(x, y))
                {
                    (b, g, r) = ((b + 160) / 2, g / 2, (r + 160) / 2);
                }

                if (foregroundOf is not null && foregroundOf.IsForeground(x, y))
                {
                    (b, g, r) = ((b + 255) / 2, (g + 220) / 2, r / 2);
                }

                var o = i * 4;
                bgra[o] = (byte)b;
                bgra[o + 1] = (byte)g;
                bgra[o + 2] = (byte)r;
                bgra[o + 3] = 255;
            }
        }
    }
}