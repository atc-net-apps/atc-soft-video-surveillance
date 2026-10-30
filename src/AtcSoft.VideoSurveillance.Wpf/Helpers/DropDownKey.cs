namespace AtcSoft.VideoSurveillance.Wpf.Helpers;

/// <summary>
/// Finds the drop-down entry for a value the server sent.
/// </summary>
public static class DropDownKey
{
    /// <summary>
    /// Gets the key of the entry that is the value. The server writes <c>topLeft</c>
    /// and <c>tcp</c>, the drop-downs have <c>TopLeft</c> and <c>tcp</c>, so the
    /// casing does not count.
    /// </summary>
    public static string For(
        string? value,
        IDictionary<string, string> items,
        string fallback)
    {
        ArgumentNullException.ThrowIfNull(items);

        return items.Keys.FirstOrDefault(key => string.Equals(key, value, StringComparison.OrdinalIgnoreCase))
               ?? fallback;
    }
}