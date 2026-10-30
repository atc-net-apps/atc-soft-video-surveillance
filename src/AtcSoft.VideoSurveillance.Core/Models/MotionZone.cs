namespace AtcSoft.VideoSurveillance.Models;

/// <summary>
/// An area of a camera's picture that motion detection treats on its own: ignored, or watched with
/// its own sensitivity. A polygon in fractions of the picture as it is shown (turned), 0 to 1 from
/// the top left, so it fits any analysis size.
/// </summary>
public sealed class MotionZone
{
    /// <summary>
    /// Gets or sets the zone's name, shown in the editor and on the timeline.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets what the zone does.
    /// </summary>
    public MotionZoneKind Kind { get; set; }

    /// <summary>
    /// Gets or sets the polygon's corners in order, at least three.
    /// </summary>
    [SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Required for JSON deserialization")]
    [SuppressMessage("Design", "MA0016:Prefer using collection abstraction instead of implementation", Justification = "Required for JSON deserialization")]
    public List<ZonePoint> Points { get; set; } = [];

    /// <summary>
    /// Gets or sets the colour by name, for the editor and the timeline.
    /// </summary>
    public string? Color { get; set; }

    /// <summary>
    /// Gets or sets a watch zone's sensitivity, 0 to 100 as the camera's; <see langword="null"/> takes the camera's.
    /// </summary>
    public int? Sensitivity { get; set; }

    /// <summary>
    /// Gets or sets the smallest object that counts in a watch zone, in percent of the picture's area;
    /// <see langword="null"/> takes the detector's default.
    /// </summary>
    public double? SmallestObjectPercent { get; set; }

    /// <summary>
    /// Creates a copy of the zone with its own list of corners.
    /// </summary>
    public MotionZone Clone()
        => new()
        {
            Name = Name,
            Kind = Kind,
            Points = [.. Points],
            Color = Color,
            Sensitivity = Sensitivity,
            SmallestObjectPercent = SmallestObjectPercent,
        };

    /// <summary>
    /// Determines whether the specified zone has the same values.
    /// </summary>
    public bool ValueEquals(MotionZone? other)
        => other is not null &&
           string.Equals(Name, other.Name, StringComparison.Ordinal) &&
           Kind == other.Kind &&
           Points.SequenceEqual(other.Points) &&
           string.Equals(Color, other.Color, StringComparison.Ordinal) &&
           Sensitivity == other.Sensitivity &&
           EqualityComparer<double?>.Default.Equals(SmallestObjectPercent, other.SmallestObjectPercent);
}