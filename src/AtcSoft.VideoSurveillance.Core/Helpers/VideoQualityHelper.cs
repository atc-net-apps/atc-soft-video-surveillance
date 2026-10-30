namespace AtcSoft.VideoSurveillance.Helpers;

/// <summary>
/// The values of the video quality setting: <c>Auto</c> (the camera's own
/// resolution) or the highest vertical resolution to decode at.
/// </summary>
public static class VideoQualityHelper
{
    /// <summary>
    /// No limit: the stream is shown at the camera's resolution.
    /// </summary>
    public const string Auto = "Auto";

    private static readonly string[] Values = [Auto, "1080p", "720p", "480p", "360p"];

    /// <summary>
    /// Gets the valid values, best quality first.
    /// </summary>
    public static IReadOnlyList<string> All => Values;

    /// <summary>
    /// Gets the canonical spelling of a video quality, or <c>null</c> when
    /// the text is not one. The older names <c>Low</c>, <c>Medium</c>,
    /// <c>High</c> and <c>Ultra</c> are understood and translated.
    /// </summary>
    /// <param name="value">The value as stored or received.</param>
    /// <returns>The canonical value, or <c>null</c>.</returns>
    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        foreach (var known in Values)
        {
            if (string.Equals(known, trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return known;
            }
        }

        return trimmed.ToUpperInvariant() switch
        {
            "LOW" => "480p",
            "MEDIUM" => "720p",
            "HIGH" => "1080p",
            "ULTRA" => Auto,
            _ => null,
        };
    }
}