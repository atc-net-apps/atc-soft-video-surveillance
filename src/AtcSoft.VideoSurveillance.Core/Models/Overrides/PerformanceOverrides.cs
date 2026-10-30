namespace AtcSoft.VideoSurveillance.Models.Overrides;

/// <summary>
/// Per-camera performance setting overrides.
/// </summary>
public class PerformanceOverrides
{
    public string? VideoQuality { get; set; }

    public bool? HardwareAcceleration { get; set; }

    public bool HasAnyOverride()
        => VideoQuality is not null ||
           HardwareAcceleration.HasValue;

    /// <inheritdoc />
    public override string ToString()
    {
        var count = new[] { VideoQuality is not null, HardwareAcceleration.HasValue }.Count(v => v);
        return $"PerformanceOverrides {{ NonNullOverrides={count.ToString(CultureInfo.InvariantCulture)} }}";
    }

    public PerformanceOverrides Clone()
        => new()
        {
            VideoQuality = VideoQuality,
            HardwareAcceleration = HardwareAcceleration,
        };

    public void CopyFrom(PerformanceOverrides? source)
    {
        VideoQuality = source?.VideoQuality;
        HardwareAcceleration = source?.HardwareAcceleration;
    }

    public bool ValueEquals(PerformanceOverrides? other)
    {
        if (other is null)
        {
            return !HasAnyOverride();
        }

        return VideoQuality == other.VideoQuality &&
               HardwareAcceleration == other.HardwareAcceleration;
    }
}