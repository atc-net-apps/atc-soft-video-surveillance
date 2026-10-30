namespace AtcSoft.VideoSurveillance.Wpf.Core.Helpers;

/// <summary>
/// The snapshot a camera's motion zones are drawn on, shown at the rotation chosen in a camera form.
/// </summary>
public static class MotionZonePicture
{
    /// <summary>
    /// Reads a snapshot.
    /// </summary>
    /// <param name="png">The snapshot as PNG (or another format WPF reads).</param>
    /// <returns>The picture, frozen.</returns>
    public static BitmapSource Decode(byte[] png)
    {
        ArgumentNullException.ThrowIfNull(png);

        using var stream = new MemoryStream(png);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    /// <summary>
    /// Shows a snapshot taken at one rotation at another.
    /// </summary>
    /// <param name="picture">The snapshot.</param>
    /// <param name="takenAt">The rotation the snapshot was taken at.</param>
    /// <param name="shownAt">The rotation chosen in the form.</param>
    /// <returns>The picture turned, frozen; the snapshot itself when the rotations are the same.</returns>
    public static BitmapSource Show(
        BitmapSource picture,
        CameraRotation takenAt,
        CameraRotation shownAt)
    {
        ArgumentNullException.ThrowIfNull(picture);

        var quarters = MotionZoneGeometry.QuarterTurns(takenAt, shownAt);
        if (quarters == 0)
        {
            return picture;
        }

        var turned = new TransformedBitmap(picture, new RotateTransform(quarters * 90));
        turned.Freeze();
        return turned;
    }
}