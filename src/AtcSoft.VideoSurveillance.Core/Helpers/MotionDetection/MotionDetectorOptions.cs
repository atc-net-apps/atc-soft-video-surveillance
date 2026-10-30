namespace AtcSoft.VideoSurveillance.Helpers.MotionDetection;

/// <summary>
/// The tuning of <see cref="BuiltInMotionDetector"/>. Sizes are relative to the picture, times
/// are in seconds, so the same options mean the same at any analysis size and rate.
/// </summary>
/// <param name="ThresholdSigmas">A pixel is foreground when it differs from the background by this many standard deviations.</param>
/// <param name="MinThreshold">…and by at least this many grey levels.</param>
/// <param name="BackgroundSeconds">How fast the background learns what stays (the time constant).</param>
/// <param name="ForegroundSeconds">How fast it takes in something that stopped moving (much slower; the time constant, so it is gone after about three).</param>
/// <param name="SmallestObjectShare">The smallest blob that counts, as a share of the picture's area.</param>
/// <param name="MergeGapShare">Blobs closer than this share of the picture's width are one object.</param>
/// <param name="LightingChangeShare">When more than this share of the picture changes at once, it is light, not an object.</param>
/// <param name="LightingSettleSeconds">How long boxes are held back after a lighting change.</param>
/// <param name="ConfirmSeconds">How long a track must be seen before it shows.</param>
/// <param name="MinTravelShare">How far a track must travel from where it began, as a share of the picture's width, before it shows.</param>
/// <param name="TrackLostSeconds">How long a track may go unseen before it ends.</param>
public sealed record MotionDetectorOptions(
    double ThresholdSigmas = 3.0,
    double MinThreshold = 16,
    double BackgroundSeconds = 4,
    double ForegroundSeconds = MotionDetectorOptions.DefaultStoppedObjectSeconds / (double)MotionDetectorOptions.StoppedObjectTimeConstants,
    double SmallestObjectShare = MotionDetectorOptions.DefaultSmallestObjectShare,
    double MergeGapShare = 0.01,
    double LightingChangeShare = 0.5,
    double LightingSettleSeconds = 1.5,
    double ConfirmSeconds = 0.4,
    double MinTravelShare = 0.03,
    double TrackLostSeconds = 1.0)
{
    /// <summary>
    /// The smallest blob that counts by default, as a share of the picture's area.
    /// </summary>
    public const double DefaultSmallestObjectShare = 0.0002;

    /// <summary>
    /// How long something that stops moving is seen at most by default, in seconds
    /// (<see cref="MotionDetectionSettings.StoppedObjectSeconds"/>).
    /// </summary>
    public const int DefaultStoppedObjectSeconds = 60;

    /// <summary>
    /// How many <see cref="ForegroundSeconds"/> the stopped-object time is: after three time
    /// constants 5 % of the difference is left, under the threshold for most of what stops, so it
    /// is background by then. Low contrast goes sooner.
    /// </summary>
    public const int StoppedObjectTimeConstants = 3;

    /// <summary>
    /// The lowest <see cref="MotionDetectionSettings.StoppedObjectSeconds"/>: below ten seconds
    /// someone standing still for a moment would vanish.
    /// </summary>
    public const int MinStoppedObjectSeconds = 10;

    /// <summary>
    /// The highest <see cref="MotionDetectionSettings.StoppedObjectSeconds"/>.
    /// </summary>
    public const int MaxStoppedObjectSeconds = 600;

    /// <summary>
    /// The highest <see cref="MotionDetectionSettings.SmallestObjectPercent"/>, in percent: the
    /// whole picture, as for a watch zone.
    /// </summary>
    public const double MaxSmallestObjectPercent = 100;

    // The share of changed picture above which a change is never light: lighting changes not ignored.
    private const double NeverLight = 1.0;

    /// <summary>
    /// The smallest object that counts by default, in percent of the picture's area: what a watch
    /// zone without its own takes, and what an editor shows for it.
    /// </summary>
    public const double DefaultSmallestObjectPercent = DefaultSmallestObjectShare * 100;

    /// <summary>
    /// Gets the options for a camera's settings: <see cref="MotionDetectionSettings.Sensitivity"/>
    /// (0 to 100) lowers the threshold as it rises; the smallest object and whether lighting
    /// changes are ignored are taken as they are, and how long something that stops is seen at most
    /// gives the foreground's time constant (<see cref="StoppedObjectTimeConstants"/>).
    /// </summary>
    /// <param name="settings">The camera's motion settings.</param>
    /// <returns>The options.</returns>
    public static MotionDetectorOptions From(MotionDetectionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var defaults = new MotionDetectorOptions();
        var (sigmas, minThreshold) = Thresholds(settings.Sensitivity);
        return defaults with
        {
            ThresholdSigmas = sigmas,
            MinThreshold = minThreshold,
            SmallestObjectShare = Math.Clamp(settings.SmallestObjectPercent, 0, MaxSmallestObjectPercent) / 100,
            ForegroundSeconds = Math.Clamp(settings.StoppedObjectSeconds, MinStoppedObjectSeconds, MaxStoppedObjectSeconds) / (double)StoppedObjectTimeConstants,
            LightingChangeShare = settings.IgnoreLightingChanges ? defaults.LightingChangeShare : NeverLight,
        };
    }

    /// <summary>
    /// Gets the thresholds for a sensitivity, 0 to 100: the camera's and a watch zone's alike.
    /// </summary>
    /// <param name="sensitivity">The sensitivity; a higher one lowers the thresholds.</param>
    /// <returns>The standard deviations and the grey levels a pixel must differ by.</returns>
    public static (double ThresholdSigmas, double MinThreshold) Thresholds(
        int sensitivity)
    {
        var share = Math.Clamp(sensitivity, 0, 100) / 100.0;
        return (3.6 - (1.6 * share), 24 - (14 * share));
    }
}