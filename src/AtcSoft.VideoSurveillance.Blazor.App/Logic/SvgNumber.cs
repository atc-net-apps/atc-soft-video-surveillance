namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// Writes a number for an SVG attribute. Razor writes a <see cref="double"/> attribute in the
/// page's language, and SVG reads only a point: "12,5" in Danish or German would not draw.
/// </summary>
public static class SvgNumber
{
    /// <summary>
    /// Writes the number with a point and at most three decimals.
    /// </summary>
    /// <param name="value">The number.</param>
    public static string Format(double value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);
}