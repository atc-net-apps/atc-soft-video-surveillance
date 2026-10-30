namespace AtcSoft.VideoSurveillance.Storybook.Scoring;

/// <summary>
/// One combination of the motion settings a person can change, tried by a sweep.
/// </summary>
/// <param name="Sensitivity">The sensitivity, 0 to 100.</param>
/// <param name="SmallestObjectPercent">The smallest object, as a percentage of the picture.</param>
/// <param name="StoppedObjectSeconds">How long something that stops is seen.</param>
public sealed record SweepSettings(
    int Sensitivity,
    double SmallestObjectPercent,
    int StoppedObjectSeconds)
{
    /// <summary>
    /// Gets the defaults the apps ship with.
    /// </summary>
    public static SweepSettings Defaults { get; } = From(new MotionDetectionSettings());

    /// <summary>
    /// Gets the combination in some settings.
    /// </summary>
    /// <param name="settings">The settings.</param>
    public static SweepSettings From(MotionDetectionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        return new(settings.Sensitivity, settings.SmallestObjectPercent, settings.StoppedObjectSeconds);
    }

    /// <summary>
    /// Gets a copy of some settings (a scene's or a review's) with this combination in it.
    /// </summary>
    /// <param name="settings">The settings.</param>
    public MotionDetectionSettings ApplyTo(MotionDetectionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var copy = JsonSerializer.Deserialize<MotionDetectionSettings>(JsonSerializer.Serialize(settings))!;
        copy.Sensitivity = Sensitivity;
        copy.SmallestObjectPercent = SmallestObjectPercent;
        copy.StoppedObjectSeconds = StoppedObjectSeconds;
        return copy;
    }

    /// <inheritdoc />
    public override string ToString()
        => string.Create(CultureInfo.InvariantCulture, $"sensitivity {Sensitivity}, smallest object {SmallestObjectPercent:0.###} %, stopped after {StoppedObjectSeconds} s");
}