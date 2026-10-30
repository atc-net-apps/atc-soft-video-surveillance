namespace BoundingBoxWpfApp;

/// <summary>
/// The detector settings in the tuning panel: the four a person sets in the apps (saved into a
/// storybook), and the inner options of <see cref="MotionDetectorOptions"/> that are not settings
/// (tried here, never saved). Times in seconds, sizes in percent.
/// </summary>
/// <param name="Sensitivity">The sensitivity, 0 to 100.</param>
/// <param name="SmallestObjectPercent">The smallest object, in percent of the picture.</param>
/// <param name="StoppedObjectSeconds">How long something that stops is seen at most.</param>
/// <param name="IgnoreLightingChanges">Whether lighting changes are ignored.</param>
/// <param name="ConfirmSeconds">How long a track is seen before it shows.</param>
/// <param name="MinTravelPercent">How far a track travels before it shows, in percent of the width.</param>
/// <param name="MergeGapPercent">Blobs closer than this, in percent of the width, are one.</param>
/// <param name="TrackLostSeconds">How long a track may go unseen.</param>
/// <param name="BackgroundSeconds">How fast the background learns what stays.</param>
/// <param name="LightingSettleSeconds">How long boxes are held back after a change of light.</param>
public sealed record SampleTuning(
    int Sensitivity,
    double SmallestObjectPercent,
    int StoppedObjectSeconds,
    bool IgnoreLightingChanges,
    double ConfirmSeconds,
    double MinTravelPercent,
    double MergeGapPercent,
    double TrackLostSeconds,
    double BackgroundSeconds,
    double LightingSettleSeconds)
{
    /// <summary>
    /// Gets the tuning of settings, with the inner options as shipped.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <returns>The tuning.</returns>
    public static SampleTuning From(MotionDetectionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var inner = new MotionDetectorOptions();
        return new SampleTuning(
            settings.Sensitivity,
            settings.SmallestObjectPercent,
            settings.StoppedObjectSeconds,
            settings.IgnoreLightingChanges,
            inner.ConfirmSeconds,
            inner.MinTravelShare * 100,
            inner.MergeGapShare * 100,
            inner.TrackLostSeconds,
            inner.BackgroundSeconds,
            inner.LightingSettleSeconds);
    }

    /// <summary>
    /// Gets settings with the tuning's four on top of others (a review's analysis size and post-motion time stay).
    /// </summary>
    /// <param name="own">The settings of the file.</param>
    /// <returns>New settings.</returns>
    public MotionDetectionSettings Settings(MotionDetectionSettings own)
    {
        ArgumentNullException.ThrowIfNull(own);

        var settings = new SweepSettings(
            Math.Clamp(Sensitivity, 0, 100),
            Math.Clamp(SmallestObjectPercent, 0, MotionDetectorOptions.MaxSmallestObjectPercent),
            Math.Clamp(StoppedObjectSeconds, MotionDetectorOptions.MinStoppedObjectSeconds, MotionDetectorOptions.MaxStoppedObjectSeconds)).ApplyTo(own);
        settings.IgnoreLightingChanges = IgnoreLightingChanges;
        return settings;
    }

    /// <summary>
    /// Gets the detector's options: those of the settings, with the inner options on top.
    /// </summary>
    /// <param name="settings">The settings (<see cref="Settings"/>).</param>
    /// <returns>The options.</returns>
    public MotionDetectorOptions Options(MotionDetectionSettings settings)
        => MotionDetectorOptions.From(settings) with
        {
            ConfirmSeconds = Math.Max(0, ConfirmSeconds),
            MinTravelShare = Math.Max(0, MinTravelPercent) / 100,
            MergeGapShare = Math.Max(0, MergeGapPercent) / 100,
            TrackLostSeconds = Math.Max(0, TrackLostSeconds),
            BackgroundSeconds = Math.Max(0.1, BackgroundSeconds),
            LightingSettleSeconds = Math.Max(0, LightingSettleSeconds),
        };
}