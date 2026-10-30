namespace AtcSoft.VideoEngine.Helpers;

/// <summary>
/// Rotates packed pixel buffers (RGB24, BGRA, ...) clockwise in quarter
/// turns. Used for frames that never pass through the GPU video processor,
/// which is where hardware-decoded frames are rotated.
/// </summary>
public static unsafe class PixelRotator
{
    /// <summary>
    /// Gets the size of a picture after it is rotated.
    /// A quarter turn swaps width and height.
    /// </summary>
    /// <param name="width">The unrotated width.</param>
    /// <param name="height">The unrotated height.</param>
    /// <param name="rotation">The clockwise rotation.</param>
    public static (int Width, int Height) GetRotatedSize(
        int width,
        int height,
        VideoRotation rotation)
        => rotation is VideoRotation.Rotate90 or VideoRotation.Rotate270
            ? (height, width)
            : (width, height);

    /// <summary>
    /// Rotates a packed pixel buffer clockwise into another buffer.
    /// </summary>
    /// <param name="source">The first byte of the source picture.</param>
    /// <param name="sourceStride">The byte length of one source row.</param>
    /// <param name="width">The source width in pixels.</param>
    /// <param name="height">The source height in pixels.</param>
    /// <param name="bytesPerPixel">The size of one pixel in bytes.</param>
    /// <param name="rotation">The clockwise rotation.</param>
    /// <param name="destination">The first byte of the destination picture, sized per <see cref="GetRotatedSize"/>.</param>
    /// <param name="destinationStride">The byte length of one destination row.</param>
    public static void Rotate(
        byte* source,
        int sourceStride,
        int width,
        int height,
        int bytesPerPixel,
        VideoRotation rotation,
        byte* destination,
        int destinationStride)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bytesPerPixel);

        switch (rotation)
        {
            case VideoRotation.Rotate90:
                // Source (x, y) lands at (height - 1 - y, x).
                for (var y = 0; y < height; y++)
                {
                    var src = source + (y * (long)sourceStride);
                    var dstColumn = destination + ((height - 1 - y) * (long)bytesPerPixel);
                    for (var x = 0; x < width; x++)
                    {
                        CopyPixel(src + (x * (long)bytesPerPixel), dstColumn + (x * (long)destinationStride), bytesPerPixel);
                    }
                }

                break;

            case VideoRotation.Rotate180:
                // Source (x, y) lands at (width - 1 - x, height - 1 - y).
                for (var y = 0; y < height; y++)
                {
                    var src = source + (y * (long)sourceStride);
                    var dstRow = destination + ((height - 1 - y) * (long)destinationStride);
                    for (var x = 0; x < width; x++)
                    {
                        CopyPixel(src + (x * (long)bytesPerPixel), dstRow + ((width - 1 - x) * (long)bytesPerPixel), bytesPerPixel);
                    }
                }

                break;

            case VideoRotation.Rotate270:
                // Source (x, y) lands at (y, width - 1 - x).
                for (var y = 0; y < height; y++)
                {
                    var src = source + (y * (long)sourceStride);
                    var dstColumn = destination + (y * (long)bytesPerPixel);
                    for (var x = 0; x < width; x++)
                    {
                        CopyPixel(src + (x * (long)bytesPerPixel), dstColumn + ((width - 1 - x) * (long)destinationStride), bytesPerPixel);
                    }
                }

                break;

            default:
                var rowBytes = width * (long)bytesPerPixel;
                for (var y = 0; y < height; y++)
                {
                    Buffer.MemoryCopy(
                        source + (y * (long)sourceStride),
                        destination + (y * (long)destinationStride),
                        rowBytes,
                        rowBytes);
                }

                break;
        }
    }

    private static void CopyPixel(
        byte* source,
        byte* destination,
        int bytesPerPixel)
    {
        if (bytesPerPixel == 4)
        {
            *(uint*)destination = *(uint*)source;
            return;
        }

        for (var i = 0; i < bytesPerPixel; i++)
        {
            destination[i] = source[i];
        }
    }
}