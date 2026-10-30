namespace AtcSoft.VideoEngine.Windows.Motion;

/// <summary>
/// <see cref="IMotionFrameReader"/> on GDI+: decodes the picture, scales it
/// down and takes each pixel's brightness.
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class GdiMotionFrameReader : IMotionFrameReader
{
    /// <inheritdoc />
    public byte[]? ReadGrayscale(
        byte[] encodedImage,
        int width,
        int height)
    {
        ArgumentNullException.ThrowIfNull(encodedImage);

        if (encodedImage.Length == 0 || width <= 0 || height <= 0)
        {
            return null;
        }

        try
        {
            using var stream = new MemoryStream(encodedImage);
            using var bitmap = new System.Drawing.Bitmap(stream);
            using var resized = new System.Drawing.Bitmap(bitmap, new System.Drawing.Size(width, height));
            return ToGrayscale(resized, width, height);
        }
        catch (ArgumentException)
        {
            // GDI+ reports bytes that are not a picture this way.
            return null;
        }
        catch (ExternalException)
        {
            return null;
        }
    }

    private static unsafe byte[] ToGrayscale(
        System.Drawing.Bitmap bitmap,
        int width,
        int height)
    {
        var grayscale = new byte[width * height];
        var data = bitmap.LockBits(
            new System.Drawing.Rectangle(0, 0, width, height),
            System.Drawing.Imaging.ImageLockMode.ReadOnly,
            System.Drawing.Imaging.PixelFormat.Format24bppRgb);

        try
        {
            var first = (byte*)data.Scan0.ToPointer();

            for (var y = 0; y < height; y++)
            {
                var row = first + (y * data.Stride);
                for (var x = 0; x < width; x++)
                {
                    // Blue, green, red. Brightness as the eye weighs them:
                    // (77 R + 150 G + 29 B) / 256 is 0.299 R + 0.587 G + 0.114 B.
                    var b = row[x * 3];
                    var g = row[(x * 3) + 1];
                    var r = row[(x * 3) + 2];
                    grayscale[(y * width) + x] = (byte)(((77 * r) + (150 * g) + (29 * b)) >> 8);
                }
            }
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        return grayscale;
    }
}