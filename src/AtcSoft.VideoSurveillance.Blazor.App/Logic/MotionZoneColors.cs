namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// A watch zone's colour, by name as every client writes it: on the zone in the editor, and on the
/// events of a recording's timeline that happened in it.
/// </summary>
public static class MotionZoneColors
{
    /// <summary>
    /// The colour of an event outside every watch zone, or in one without a colour.
    /// </summary>
    public const string Event = "#FF8C00";

    /// <summary>
    /// Gets the colours of a camera's watch zones that have one, by zone name, as a browser takes
    /// them; of two zones with one name, the first counts.
    /// </summary>
    /// <param name="zones">The camera's zones.</param>
    public static IReadOnlyDictionary<string, string> ByZone(
        IEnumerable<MotionZone>? zones)
    {
        var colours = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var zone in zones ?? [])
        {
            if (zone.Kind.Value == MotionZoneKind.Names.Watch &&
                !string.IsNullOrEmpty(zone.Name) &&
                !string.IsNullOrWhiteSpace(zone.Color) &&
                !colours.ContainsKey(zone.Name))
            {
                colours[zone.Name] = ColorChoices.ToCss(zone.Color);
            }
        }

        return colours;
    }

    /// <summary>
    /// Gets the colour an event is drawn in: its zone's, else <see cref="Event"/>.
    /// </summary>
    /// <param name="zone">The event's zone, or <see langword="null"/>.</param>
    /// <param name="colours">The zones' colours (see <see cref="ByZone"/>).</param>
    public static string ForEvent(
        string? zone,
        IReadOnlyDictionary<string, string>? colours)
        => zone is not null && colours?.TryGetValue(zone, out var colour) == true
            ? colour
            : Event;
}