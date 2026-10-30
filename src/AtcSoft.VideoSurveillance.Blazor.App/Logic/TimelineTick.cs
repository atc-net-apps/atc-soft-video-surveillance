namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// A mark under the Timeline page's lanes.
/// </summary>
/// <param name="Fraction">Where it is, 0 to 1 of the span.</param>
/// <param name="Label">What it says: a time, or a day for a span of days.</param>
public sealed record TimelineTick(
    double Fraction,
    string Label)
{
    /// <summary>
    /// Gets where the mark goes, as a CSS left position.
    /// </summary>
    public string Left
        => SvgNumber.Format(Fraction * 100) + "%";
}