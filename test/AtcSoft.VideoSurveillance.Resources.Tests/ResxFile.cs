namespace AtcSoft.VideoSurveillance.Resources;

/// <summary>
/// Reads one of the Translations files as key -> value.
/// </summary>
internal static class ResxFile
{
    public static string PathOf(string culture)
        => Path.Combine(
            RepositoryFiles.Root,
            "src",
            "AtcSoft.VideoSurveillance.Resources",
            culture.Length == 0 ? "Translations.resx" : $"Translations.{culture}.resx");

    public static IReadOnlyDictionary<string, string> Read(string culture)
        => XDocument.Load(PathOf(culture))
            .Root!
            .Elements("data")
            .ToDictionary(
                data => (string)data.Attribute("name")!,
                data => (string?)data.Element("value") ?? string.Empty,
                StringComparer.Ordinal);

    /// <summary>
    /// Gets every key of a file as written, duplicates included (<see cref="Read"/> would fail on them).
    /// </summary>
    public static IReadOnlyList<string> Names(string culture)
        => [.. XDocument.Load(PathOf(culture)).Root!.Elements("data").Select(data => (string)data.Attribute("name")!)];
}