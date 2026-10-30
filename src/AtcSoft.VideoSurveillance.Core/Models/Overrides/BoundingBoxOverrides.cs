namespace AtcSoft.VideoSurveillance.Models.Overrides;

/// <summary>
/// Per-camera bounding box setting overrides.
/// </summary>
public class BoundingBoxOverrides
{
    public bool? ShowInGrid { get; set; }

    public bool? ShowInFullScreen { get; set; }

    public string? Color { get; set; }

    public int? Thickness { get; set; }

    public double? Smoothing { get; set; }

    public int? MinArea { get; set; }

    public int? Padding { get; set; }

    public bool HasAnyOverride()
        => ShowInGrid.HasValue ||
           ShowInFullScreen.HasValue ||
           Color is not null ||
           Thickness.HasValue ||
           Smoothing.HasValue ||
           MinArea.HasValue ||
           Padding.HasValue;

    /// <inheritdoc />
    public override string ToString()
    {
        var count = new[] { ShowInGrid.HasValue, ShowInFullScreen.HasValue, Color is not null, Thickness.HasValue, Smoothing.HasValue, MinArea.HasValue, Padding.HasValue }.Count(v => v);
        return $"BoundingBoxOverrides {{ NonNullOverrides={count.ToString(CultureInfo.InvariantCulture)} }}";
    }

    public BoundingBoxOverrides Clone()
        => new()
        {
            ShowInGrid = ShowInGrid,
            ShowInFullScreen = ShowInFullScreen,
            Color = Color,
            Thickness = Thickness,
            Smoothing = Smoothing,
            MinArea = MinArea,
            Padding = Padding,
        };

    public void CopyFrom(BoundingBoxOverrides? source)
    {
        ShowInGrid = source?.ShowInGrid;
        ShowInFullScreen = source?.ShowInFullScreen;
        Color = source?.Color;
        Thickness = source?.Thickness;
        Smoothing = source?.Smoothing;
        MinArea = source?.MinArea;
        Padding = source?.Padding;
    }

    public bool ValueEquals(BoundingBoxOverrides? other)
    {
        if (other is null)
        {
            return !HasAnyOverride();
        }

        return ShowInGrid == other.ShowInGrid &&
               ShowInFullScreen == other.ShowInFullScreen &&
               Color == other.Color &&
               Thickness == other.Thickness &&
               Smoothing.IsEqual(other.Smoothing) &&
               MinArea == other.MinArea &&
               Padding == other.Padding;
    }
}