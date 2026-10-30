namespace AtcSoft.VideoSurveillance.Wpf.Core.Helpers;

/// <summary>
/// Picks a layout by its number in the layout list, for the Ctrl+1 to
/// Ctrl+9 shortcuts.
/// </summary>
public static class LayoutQuickSelect
{
    /// <summary>
    /// Gets the layout in the given place of the list, counting from 1.
    /// </summary>
    /// <param name="layouts">The layouts, in the order they are listed.</param>
    /// <param name="number">The place in the list, from 1.</param>
    /// <returns>The layout, or <c>null</c> when there is none in that place.</returns>
    public static CameraLayout? GetByNumber(
        IReadOnlyList<CameraLayout> layouts,
        int number)
    {
        ArgumentNullException.ThrowIfNull(layouts);

        return number >= 1 && number <= layouts.Count
            ? layouts[number - 1]
            : null;
    }

    /// <summary>
    /// Gets the layout for a number given as text, as a key binding passes it.
    /// </summary>
    /// <param name="layouts">The layouts, in the order they are listed.</param>
    /// <param name="number">The place in the list, from 1, as text.</param>
    /// <returns>The layout, or <c>null</c> when the text is not a number or there is no layout in that place.</returns>
    public static CameraLayout? GetByNumber(
        IReadOnlyList<CameraLayout> layouts,
        string? number)
        => int.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? GetByNumber(layouts, value)
            : null;
}