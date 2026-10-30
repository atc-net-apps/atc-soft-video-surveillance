namespace AtcSoft.VideoSurveillance.Models.Overrides;

/// <summary>
/// Per-camera motion detection setting overrides.
/// </summary>
public class MotionDetectionOverrides
{
    public int? Sensitivity { get; set; }

    public double? SmallestObjectPercent { get; set; }

    public int? StoppedObjectSeconds { get; set; }

    public bool? IgnoreLightingChanges { get; set; }

    public double? MinimumChangePercent { get; set; }

    public int? AnalysisFrameRate { get; set; }

    public int? AnalysisWidth { get; set; }

    public int? AnalysisHeight { get; set; }

    public int? PostMotionDurationSeconds { get; set; }

    public int? CooldownSeconds { get; set; }

    public BoundingBoxOverrides BoundingBox { get; set; } = new();

    public bool HasAnyOverride()
        => Sensitivity.HasValue ||
           SmallestObjectPercent.HasValue ||
           StoppedObjectSeconds.HasValue ||
           IgnoreLightingChanges.HasValue ||
           MinimumChangePercent.HasValue ||
           AnalysisFrameRate.HasValue ||
           AnalysisWidth.HasValue ||
           AnalysisHeight.HasValue ||
           PostMotionDurationSeconds.HasValue ||
           CooldownSeconds.HasValue ||
           BoundingBox.HasAnyOverride();

    /// <inheritdoc />
    public override string ToString()
    {
        var count = new[] { Sensitivity.HasValue, SmallestObjectPercent.HasValue, StoppedObjectSeconds.HasValue, IgnoreLightingChanges.HasValue, MinimumChangePercent.HasValue, AnalysisFrameRate.HasValue, AnalysisWidth.HasValue, AnalysisHeight.HasValue, PostMotionDurationSeconds.HasValue, CooldownSeconds.HasValue, BoundingBox.HasAnyOverride() }.Count(v => v);
        return $"MotionDetectionOverrides {{ NonNullOverrides={count.ToString(CultureInfo.InvariantCulture)} }}";
    }

    public MotionDetectionOverrides Clone()
        => new()
        {
            Sensitivity = Sensitivity,
            SmallestObjectPercent = SmallestObjectPercent,
            StoppedObjectSeconds = StoppedObjectSeconds,
            IgnoreLightingChanges = IgnoreLightingChanges,
            MinimumChangePercent = MinimumChangePercent,
            AnalysisFrameRate = AnalysisFrameRate,
            AnalysisWidth = AnalysisWidth,
            AnalysisHeight = AnalysisHeight,
            PostMotionDurationSeconds = PostMotionDurationSeconds,
            CooldownSeconds = CooldownSeconds,
            BoundingBox = BoundingBox.Clone(),
        };

    public void CopyFrom(MotionDetectionOverrides? source)
    {
        Sensitivity = source?.Sensitivity;
        SmallestObjectPercent = source?.SmallestObjectPercent;
        StoppedObjectSeconds = source?.StoppedObjectSeconds;
        IgnoreLightingChanges = source?.IgnoreLightingChanges;
        MinimumChangePercent = source?.MinimumChangePercent;
        AnalysisFrameRate = source?.AnalysisFrameRate;
        AnalysisWidth = source?.AnalysisWidth;
        AnalysisHeight = source?.AnalysisHeight;
        PostMotionDurationSeconds = source?.PostMotionDurationSeconds;
        CooldownSeconds = source?.CooldownSeconds;
        BoundingBox.CopyFrom(source?.BoundingBox);
    }

    public bool ValueEquals(MotionDetectionOverrides? other)
    {
        if (other is null)
        {
            return !HasAnyOverride();
        }

        return Sensitivity == other.Sensitivity &&
               SmallestObjectPercent.IsEqual(other.SmallestObjectPercent) &&
               StoppedObjectSeconds == other.StoppedObjectSeconds &&
               IgnoreLightingChanges == other.IgnoreLightingChanges &&
               MinimumChangePercent.IsEqual(other.MinimumChangePercent) &&
               AnalysisFrameRate == other.AnalysisFrameRate &&
               AnalysisWidth == other.AnalysisWidth &&
               AnalysisHeight == other.AnalysisHeight &&
               PostMotionDurationSeconds == other.PostMotionDurationSeconds &&
               CooldownSeconds == other.CooldownSeconds &&
               BoundingBox.ValueEquals(other.BoundingBox);
    }
}