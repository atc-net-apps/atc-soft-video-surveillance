namespace AtcSoft.VideoSurveillance.Helpers;

/// <summary>
/// Maps motion bounding boxes from analysis coordinates to the place the
/// video is actually drawn inside a container. The video is fitted uniformly
/// and centred, so a container with another aspect ratio shows black bars
/// that the boxes must not spill into.
/// </summary>
public static class MotionBoxMapper
{
    /// <summary>
    /// Gets the size a video is displayed at once the rotation is applied.
    /// A quarter turn swaps width and height.
    /// </summary>
    /// <param name="width">The unrotated video width.</param>
    /// <param name="height">The unrotated video height.</param>
    /// <param name="rotation">The clockwise rotation applied for display.</param>
    public static (int Width, int Height) GetDisplaySize(
        int width,
        int height,
        CameraRotation rotation)
        => rotation is CameraRotation.Rotate90 or CameraRotation.Rotate270
            ? (height, width)
            : (width, height);

    /// <summary>
    /// Calculates the area the video occupies inside the container.
    /// </summary>
    /// <param name="containerWidth">The container width.</param>
    /// <param name="containerHeight">The container height.</param>
    /// <param name="displayWidth">The displayed (rotated) video width; 0 when unknown.</param>
    /// <param name="displayHeight">The displayed (rotated) video height; 0 when unknown.</param>
    /// <returns>The video area. When the video size is unknown it is the full container.</returns>
    public static BoundingBox CalculateVideoArea(
        double containerWidth,
        double containerHeight,
        int displayWidth,
        int displayHeight)
    {
        if (displayWidth <= 0 || displayHeight <= 0 || containerWidth <= 0 || containerHeight <= 0)
        {
            return new BoundingBox { Width = containerWidth, Height = containerHeight };
        }

        var scale = Math.Min(containerWidth / displayWidth, containerHeight / displayHeight);
        var width = displayWidth * scale;
        var height = displayHeight * scale;

        return new BoundingBox
        {
            X = (containerWidth - width) / 2,
            Y = (containerHeight - height) / 2,
            Width = width,
            Height = height,
        };
    }

    /// <summary>
    /// Maps a box from analysis coordinates into the video area.
    /// </summary>
    /// <param name="box">The box in analysis coordinates.</param>
    /// <param name="analysisWidth">The analysis frame width.</param>
    /// <param name="analysisHeight">The analysis frame height.</param>
    /// <param name="videoArea">The area from <see cref="CalculateVideoArea"/>.</param>
    public static BoundingBox MapToVideoArea(
        BoundingBox box,
        int analysisWidth,
        int analysisHeight,
        BoundingBox videoArea)
    {
        ArgumentNullException.ThrowIfNull(box);
        ArgumentNullException.ThrowIfNull(videoArea);

        if (analysisWidth <= 0 || analysisHeight <= 0)
        {
            return new BoundingBox { X = videoArea.X, Y = videoArea.Y, TrackId = box.TrackId };
        }

        var scaleX = videoArea.Width / analysisWidth;
        var scaleY = videoArea.Height / analysisHeight;

        return new BoundingBox
        {
            X = (box.X * scaleX) + videoArea.X,
            Y = (box.Y * scaleY) + videoArea.Y,
            Width = box.Width * scaleX,
            Height = box.Height * scaleY,
            TrackId = box.TrackId,
        };
    }
}