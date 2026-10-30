namespace BoundingBoxWpfApp;

/// <summary>
/// How the sample makes a detector: the settings and inner options from the tuning panel, the
/// zones of a recording's review, and the warm-up of <i>Analyse recording</i>. It keeps its
/// diagnostics for the debug layers, unless asked not to.
/// </summary>
/// <param name="Settings">The settings.</param>
/// <param name="Options">The options; those of <paramref name="Settings"/> when <see langword="null"/>.</param>
/// <param name="Zones">The zones, or <see langword="null"/>.</param>
/// <param name="WarmUpSeconds">How long the detector only learns at the start.</param>
public sealed record SampleDetector(
    MotionDetectionSettings Settings,
    MotionDetectorOptions? Options = null,
    IReadOnlyList<MotionZone>? Zones = null,
    double WarmUpSeconds = 0)
{
    /// <summary>
    /// Makes a fresh detector.
    /// </summary>
    /// <param name="keepDiagnostics">Whether it keeps its diagnostics: for the debug layers; the live clock does not show them.</param>
    /// <returns>The detector.</returns>
    public BuiltInMotionDetector Create(bool keepDiagnostics = true)
        => new(Settings, Options, Zones)
        {
            WarmUpSeconds = WarmUpSeconds,
            KeepDiagnostics = keepDiagnostics,
        };
}