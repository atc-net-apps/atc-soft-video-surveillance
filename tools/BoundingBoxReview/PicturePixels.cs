namespace BoundingBoxReview;

/// <summary>
/// PNG files as BGRA pixels and back, with WPF's codecs (no FFmpeg needed).
/// </summary>
public static class PicturePixels
{
    public static (byte[] Pixels, int Width, int Height) Load(string path)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(path);
        image.EndInit();
        var bgra = new FormatConvertedBitmap(image, PixelFormats.Bgra32, destinationPalette: null, alphaThreshold: 0);
        var pixels = new byte[bgra.PixelWidth * bgra.PixelHeight * 4];
        bgra.CopyPixels(pixels, bgra.PixelWidth * 4, 0);
        return (pixels, bgra.PixelWidth, bgra.PixelHeight);
    }

    public static BitmapSource ToImage(
        byte[] pixels,
        int width,
        int height)
    {
        var image = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, palette: null, pixels, width * 4);
        image.Freeze();
        return image;
    }

    public static void Save(
        string path,
        byte[] pixels,
        int width,
        int height)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(ToImage(pixels, width, height)));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}