namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// Where the motion boxes go in a live tile. The server finds motion in the
/// picture as it is shown (turned the way the camera is set up), and the
/// picture is fitted into its tile with bars where their shapes differ.
/// The boxes are laid over the picture, so they need its shape.
/// </summary>
public static class MotionOverlay
{
    /// <summary>
    /// Gets the size of the picture as it is shown: what the camera sends,
    /// standing upright when the camera is turned a quarter.
    /// </summary>
    /// <param name="videoWidth">The width of the video the browser plays.</param>
    /// <param name="videoHeight">Its height.</param>
    /// <param name="rotation">How the camera is turned.</param>
    /// <returns>The size, or <see langword="null"/> while the video has none yet.</returns>
    public static (int Width, int Height)? ShownSize(
        int videoWidth,
        int videoHeight,
        CameraRotation? rotation)
    {
        if (videoWidth <= 0 || videoHeight <= 0)
        {
            return null;
        }

        return rotation?.Value is CameraRotation.Names.Rotate90 or CameraRotation.Names.Rotate270
            ? (videoHeight, videoWidth)
            : (videoWidth, videoHeight);
    }

    /// <summary>
    /// Gets the size of the picture as it is shown from what the browser
    /// reports about the video element (width, then height).
    /// </summary>
    public static (int Width, int Height)? ShownSize(
        int[]? videoSize,
        CameraRotation? rotation)
        => videoSize is { Length: 2 }
            ? ShownSize(videoSize[0], videoSize[1], rotation)
            : null;
}