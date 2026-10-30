namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// The colours a setting can have: the sixteen basic colours, by name, as
/// the desktop apps offer them. Settings are shared between the clients, so
/// they have to agree on how a colour is written.
/// </summary>
public static class ColorChoices
{
    private static readonly (string Name, string Hex)[] Basic =
    [
        ("Aqua", "#00FFFF"),
        ("Black", "#000000"),
        ("Blue", "#0000FF"),
        ("Fuchsia", "#FF00FF"),
        ("Gray", "#808080"),
        ("Green", "#008000"),
        ("Lime", "#00FF00"),
        ("Maroon", "#800000"),
        ("Navy", "#000080"),
        ("Olive", "#808000"),
        ("Purple", "#800080"),
        ("Red", "#FF0000"),
        ("Silver", "#C0C0C0"),
        ("Teal", "#008080"),
        ("White", "#FFFFFF"),
        ("Yellow", "#FFFF00"),
    ];

    /// <summary>
    /// Gets the names of the basic colours, sorted.
    /// </summary>
    public static IReadOnlyList<string> Names { get; } = [.. Basic.Select(c => c.Name)];

    /// <summary>
    /// Gets a colour's name as the page shows it, in the chosen language. The
    /// value on the wire stays the English name; a colour set by hand (#RRGGBB)
    /// is shown as it is.
    /// </summary>
    public static string Label(string name)
        => name switch
        {
            "Aqua" => Translations.ColourAqua,
            "Black" => Translations.ColourBlack,
            "Blue" => Translations.ColourBlue,
            "Fuchsia" => Translations.ColourFuchsia,
            "Gray" => Translations.ColourGray,
            "Green" => Translations.ColourGreen,
            "Lime" => Translations.ColourLime,
            "Maroon" => Translations.ColourMaroon,
            "Navy" => Translations.ColourNavy,
            "Olive" => Translations.ColourOlive,
            "Purple" => Translations.ColourPurple,
            "Red" => Translations.ColourRed,
            "Silver" => Translations.ColourSilver,
            "Teal" => Translations.ColourTeal,
            "White" => Translations.ColourWhite,
            "Yellow" => Translations.ColourYellow,
            _ => name,
        };

    /// <summary>
    /// Gets a colour the way it is stored: the name of a basic colour where
    /// the value is one (by name in any casing, or by its hex value), the
    /// fallback where there is no value, and anything else unchanged.
    /// </summary>
    public static string Normalize(
        string? value,
        string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var trimmed = value.Trim();
        foreach (var (name, hex) in Basic)
        {
            if (string.Equals(name, trimmed, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(hex, WithoutAlpha(trimmed), StringComparison.OrdinalIgnoreCase))
            {
                return name;
            }
        }

        return value;
    }

    /// <summary>
    /// Gets a colour as a value a browser understands.
    /// </summary>
    public static string ToCss(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "inherit";
        }

        foreach (var (name, hex) in Basic)
        {
            if (string.Equals(name, value.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return hex;
            }
        }

        return value;
    }

    // "#AARRGGBB", as the desktop apps may write a colour, to "#RRGGBB".
    private static string WithoutAlpha(string value)
        => value.Length == 9 && value[0] == '#' && value.StartsWith("#FF", StringComparison.OrdinalIgnoreCase)
            ? "#" + value[3..]
            : value;
}