namespace AtcSoft.VideoSurveillance.Storybook.Model;

/// <summary>
/// The detector settings the scene is meant for, where they differ from the defaults: a door
/// camera, where someone fills most of the picture, does not ignore lighting changes. Each is the
/// default's when not given.
/// </summary>
public sealed class StorybookDetector
{
    /// <summary>
    /// Gets or sets the sensitivity, 0 to 100.
    /// </summary>
    public int? Sensitivity { get; set; }

    /// <summary>
    /// Gets or sets the smallest object, in percent of the picture.
    /// </summary>
    public double? SmallestObjectPercent { get; set; }

    /// <summary>
    /// Gets or sets how long something that stops is seen at most, in seconds.
    /// </summary>
    public int? StoppedObjectSeconds { get; set; }

    /// <summary>
    /// Gets or sets whether lighting changes are ignored.
    /// </summary>
    public bool? IgnoreLightingChanges { get; set; }

    /// <summary>
    /// Gets the settings to run the scene with: the defaults, with what the scene gives.
    /// </summary>
    /// <returns>New settings.</returns>
    public MotionDetectionSettings Settings()
    {
        var settings = new MotionDetectionSettings();
        if (Sensitivity is { } sensitivity)
        {
            settings.Sensitivity = sensitivity;
        }

        if (SmallestObjectPercent is { } smallest)
        {
            settings.SmallestObjectPercent = smallest;
        }

        if (StoppedObjectSeconds is { } stopped)
        {
            settings.StoppedObjectSeconds = stopped;
        }

        if (IgnoreLightingChanges is { } ignore)
        {
            settings.IgnoreLightingChanges = ignore;
        }

        return settings;
    }

    /// <summary>
    /// Gets the storybook's view of settings: only what differs from the defaults, so a storybook
    /// saved with the defaults says nothing about the detector.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <returns>The detector section.</returns>
    public static StorybookDetector From(MotionDetectionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var defaults = new MotionDetectionSettings();
        return new StorybookDetector
        {
            Sensitivity = settings.Sensitivity != defaults.Sensitivity ? settings.Sensitivity : null,
            SmallestObjectPercent = Math.Abs(settings.SmallestObjectPercent - defaults.SmallestObjectPercent) > 1e-9 ? settings.SmallestObjectPercent : null,
            StoppedObjectSeconds = settings.StoppedObjectSeconds != defaults.StoppedObjectSeconds ? settings.StoppedObjectSeconds : null,
            IgnoreLightingChanges = settings.IgnoreLightingChanges != defaults.IgnoreLightingChanges ? settings.IgnoreLightingChanges : null,
        };
    }
}