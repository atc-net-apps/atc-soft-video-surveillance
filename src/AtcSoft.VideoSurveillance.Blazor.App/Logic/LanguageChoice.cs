namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// The web client's language: the one chosen in this browser, else the
/// browser's own when the app has it, else US English.
/// </summary>
public static class LanguageChoice
{
    public const string StorageKey = "language";

    private const string Default = "en-US";

    /// <summary>
    /// Gets the languages, each named in itself.
    /// </summary>
    public static IReadOnlyList<(string Culture, string Label)> Supported { get; } =
    [
        ("en-US", "English (United States)"),
        ("en-GB", "English (United Kingdom)"),
        ("da-DK", "Dansk"),
        ("de-DE", "Deutsch"),
    ];

    public static string Resolve(
        string? stored,
        string? browser)
    {
        if (stored is not null && Supported.Any(language => string.Equals(language.Culture, stored, StringComparison.Ordinal)))
        {
            return stored;
        }

        if (string.IsNullOrEmpty(browser))
        {
            return Default;
        }

        if (string.Equals(browser, "en-GB", StringComparison.OrdinalIgnoreCase))
        {
            return "en-GB";
        }

        return browser.Split('-')[0].ToUpperInvariant() switch
        {
            "DA" => "da-DK",
            "DE" => "de-DE",
            _ => Default,
        };
    }
}