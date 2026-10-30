namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// The choices the settings page offers. They are the same choices, in the
/// same order, as the desktop apps' settings dialog, so a value picked in
/// one client is one the others can show. The labels are in the language the
/// app started in (Program.cs sets it before any page reads them).
/// </summary>
public static class SettingsOptions
{
    public static IReadOnlyList<(string Value, string Label)> VideoQualities { get; } =
    [
        ("Auto", Translations.VideoQualityAuto),
        ("1080p", "1080p"),
        ("720p", "720p"),
        ("480p", "480p"),
        ("360p", "360p"),
    ];

    public static IReadOnlyList<(double Value, string Label)> OverlayOpacities { get; } =
        [.. Enumerable.Range(0, 11).Select(i => (i / 10.0, $"{i * 10}%"))];

    public static IReadOnlyList<(int Value, string Label)> MotionSensitivities { get; } =
    [
        (10, Translations.MotionSensitivityLow),
        (30, Translations.MotionSensitivityMedium),
        (50, Translations.MotionSensitivityHigh),
        (70, Translations.MotionSensitivityVeryHigh),
    ];

    // The longer side of the analysed picture; the other side follows the camera's picture.
    public static IReadOnlyList<(int Value, string Label)> AnalysisWidths { get; } =
    [
        (320, Translations.MotionResolutionLowCpu),
        (480, Translations.MotionResolutionBalanced),
        (640, Translations.MotionResolutionBetterDetection),
        (800, Translations.MotionResolutionHighCpu),
        (960, Translations.MotionResolutionVeryHighCpu),
    ];

    public static IReadOnlyList<(int Value, string Label)> PostMotionDurations { get; } =
    [
        (5, Translations.Duration5Seconds),
        (10, Translations.Duration10Seconds),
        (15, Translations.Duration15Seconds),
        (30, Translations.Duration30Seconds),
        (60, Translations.Duration1Minute),
    ];

    public static IReadOnlyList<(int Value, string Label)> BoundingBoxThicknesses { get; } =
        [.. Enumerable.Range(1, 5).Select(i => (i, $"{i} px"))];

    public static IReadOnlyList<(int Value, string Label)> ThumbnailTiles { get; } =
    [
        (1, Translations.ThumbnailTileOne),
        (4, Translations.ThumbnailTileFour),
    ];

    public static IReadOnlyList<(int Value, string Label)> MaxRecordingDurations { get; } =
    [
        (15, Translations.Duration15Minutes),
        (30, Translations.Duration30Minutes),
        (60, Translations.Duration1Hour),
        (120, Translations.Duration2Hours),
        (180, Translations.Duration3Hours),
        (240, Translations.Duration4Hours),
    ];

    public static IReadOnlyList<(int Value, string Label)> RetentionPeriods { get; } =
    [
        (7, Translations.RetentionPeriod7Days),
        (14, Translations.RetentionPeriod14Days),
        (30, Translations.RetentionPeriod30Days),
        (60, Translations.RetentionPeriod60Days),
        (90, Translations.RetentionPeriod90Days),
        (180, Translations.RetentionPeriod6Months),
        (365, Translations.RetentionPeriod1Year),
    ];

    public static IReadOnlyList<(int Value, string Label)> MinFreeSpaces { get; } =
    [
        (512, "500 MB"),
        (1024, "1 GB"),
        (2048, string.Format(CultureInfo.CurrentCulture, Translations.RecommendedChoice1, "2 GB")),
        (5120, "5 GB"),
        (10240, "10 GB"),
        (20480, "20 GB"),
    ];

    public static IReadOnlyList<(int Value, string Label)> MaxRecordingStorages { get; } =
    [
        (0, Translations.NoLimit),
        (50, "50 GB"),
        (100, "100 GB"),
        (250, "250 GB"),
        (500, "500 GB"),
        (1000, "1 TB"),
        (2000, "2 TB"),
        (4000, "4 TB"),
    ];

    public static IReadOnlyList<(string Value, string Label)> TimelapseIntervals { get; } =
    [
        ("10s", Translations.Duration10Seconds),
        ("30s", Translations.Duration30Seconds),
        ("1m", Translations.Duration1Minute),
        ("5m", Translations.Duration5Minutes),
        ("10m", Translations.Duration10Minutes),
        ("30m", Translations.Duration30Minutes),
        ("1h", Translations.Duration1Hour),
        ("3h", Translations.Duration3Hours),
        ("6h", Translations.Duration6Hours),
        ("12h", Translations.Duration12Hours),
        ("24h", Translations.Duration24Hours),
    ];

    /// <summary>
    /// Returns the choices, with the current value added when it is not one
    /// of them. A value set by another client, or by hand in the settings
    /// file, is then shown as it is instead of as an empty box.
    /// </summary>
    /// <typeparam name="T">The type of the setting's value.</typeparam>
    public static IReadOnlyList<(T Value, string Label)> WithCurrent<T>(
        IReadOnlyList<(T Value, string Label)> options,
        T current,
        Func<T, string> describe)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(describe);

        if (current is null || options.Any(o => EqualityComparer<T>.Default.Equals(o.Value, current)))
        {
            return options;
        }

        return [.. options, (current, describe(current))];
    }
}