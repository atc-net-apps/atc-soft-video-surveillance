namespace AtcSoft.VideoSurveillance.Services;

/// <summary>
/// Turns a picture captured from a camera into the small grayscale picture
/// motion detection compares.
/// </summary>
public interface IMotionFrameReader
{
    /// <summary>
    /// Reads an encoded picture (PNG, as a pipeline captures it) as a
    /// grayscale picture of the given size.
    /// </summary>
    /// <param name="encodedImage">The picture file's bytes.</param>
    /// <param name="width">The width to scale it to.</param>
    /// <param name="height">The height to scale it to.</param>
    /// <returns>One byte per pixel, row by row; <see langword="null"/> when the bytes are not a picture.</returns>
    byte[]? ReadGrayscale(
        byte[] encodedImage,
        int width,
        int height);
}