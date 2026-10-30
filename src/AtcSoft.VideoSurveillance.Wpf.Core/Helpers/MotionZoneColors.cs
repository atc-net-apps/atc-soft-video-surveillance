namespace AtcSoft.VideoSurveillance.Wpf.Core.Helpers;

/// <summary>
/// A watch zone's colour, by name as every client writes it: on the zone in the editor, and on the
/// events of a recording's timeline that happened in it.
/// </summary>
public static class MotionZoneColors
{
    /// <summary>
    /// Gets a colour's brush by its name (<c>Green</c>) or #RRGGBB.
    /// </summary>
    /// <param name="color">The colour.</param>
    /// <returns>The brush, frozen; <see langword="null"/> for none or one WPF does not know.</returns>
    public static SolidColorBrush? Brush(string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
        {
            return null;
        }

        try
        {
            if (ColorConverter.ConvertFromString(color) is not Color value)
            {
                return null;
            }

            var brush = new SolidColorBrush(value);
            brush.Freeze();
            return brush;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>
    /// Gets the brushes of a camera's watch zones that have a colour, by zone name; of two zones
    /// with one name, the first counts.
    /// </summary>
    /// <param name="zones">The camera's zones.</param>
    public static IReadOnlyDictionary<string, Brush> ByZone(
        IEnumerable<MotionZone>? zones)
    {
        var brushes = new Dictionary<string, Brush>(StringComparer.Ordinal);
        foreach (var zone in zones ?? [])
        {
            if (zone.Kind == MotionZoneKind.Watch &&
                !string.IsNullOrEmpty(zone.Name) &&
                !brushes.ContainsKey(zone.Name) &&
                Brush(zone.Color) is { } brush)
            {
                brushes[zone.Name] = brush;
            }
        }

        return brushes;
    }
}