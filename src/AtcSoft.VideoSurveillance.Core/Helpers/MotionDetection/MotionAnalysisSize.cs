namespace AtcSoft.VideoSurveillance.Helpers.MotionDetection;

/// <summary>
/// The size a camera's picture is analysed at: its longer side as set
/// (<see cref="MotionDetectionSettings.AnalysisWidth"/>), the other side following the picture's
/// shape as displayed, so a wide camera is not squeezed into 4:3 and a camera turned upright costs
/// no more than one lying down. Live detection and <c>Analyse recording</c> both use it, so their
/// boxes and activity indexes agree.
/// </summary>
public static class MotionAnalysisSize
{
    /// <summary>
    /// The longer side when none is set, in pixels.
    /// </summary>
    public const int DefaultLongerSide = 800;

    /// <summary>
    /// The shortest a side gets, in pixels.
    /// </summary>
    public const int MinSide = 16;

    /// <summary>
    /// Gets the analysis size for a picture.
    /// </summary>
    /// <param name="longerSide">The longer side as set; 0 or less takes <see cref="DefaultLongerSide"/>.</param>
    /// <param name="displayWidth">The picture's width as displayed (turned); 0 when not known yet.</param>
    /// <param name="displayHeight">The picture's height as displayed; 0 when not known yet.</param>
    /// <returns>The width and height; 4:3 lying down while the picture's size is not known.</returns>
    public static (int Width, int Height) For(
        int longerSide,
        int displayWidth,
        int displayHeight)
    {
        var longer = Math.Max(longerSide > 0 ? longerSide : DefaultLongerSide, MinSide);
        if (displayWidth <= 0 || displayHeight <= 0)
        {
            return (longer, Shorter(longer, 3, 4));
        }

        return displayWidth >= displayHeight
            ? (longer, Shorter(longer, displayHeight, displayWidth))
            : (Shorter(longer, displayWidth, displayHeight), longer);
    }

    // The shorter side for the longer one, rounded to whole pixels.
    private static int Shorter(
        int longer,
        int shortSide,
        int longSide)
        => Math.Max((int)Math.Round(longer * (double)shortSide / longSide, MidpointRounding.AwayFromZero), MinSide);
}