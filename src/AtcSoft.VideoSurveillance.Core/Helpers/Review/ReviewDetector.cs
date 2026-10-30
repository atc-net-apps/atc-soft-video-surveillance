namespace AtcSoft.VideoSurveillance.Helpers.Review;

/// <summary>
/// How a reviewed recording is analysed: as *Analyse recording* does, with the camera's settings
/// and zones as they were when it was reviewed (the zones turned to the recording's rotation).
/// </summary>
public sealed class ReviewDetector
{
    /// <summary>
    /// The shipped detector's name.
    /// </summary>
    public const string BuiltIn = "builtIn";

    /// <summary>
    /// Gets or sets the detector's name.
    /// </summary>
    public string Name { get; set; } = BuiltIn;

    /// <summary>
    /// Gets or sets the camera's motion settings.
    /// </summary>
    public MotionDetectionSettings Settings { get; set; } = new();

    /// <summary>
    /// Gets the camera's motion zones, turned to the recording's rotation.
    /// </summary>
    public IList<MotionZone> Zones { get; init; } = [];
}