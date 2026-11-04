namespace AtcSoft.Video.Surveillance.Wpf.Core.Models;

/// <summary>
/// A span a timeline offers (CameraWall's and the desktop client's): the last hours up to now, or a chosen day (<see cref="Hours"/> 0).
/// </summary>
/// <param name="Hours">How many hours back from now; 0 for a chosen day.</param>
/// <param name="Label">What the selector shows.</param>
public sealed record TimelineSpanChoice(
    int Hours,
    string Label)
{
    /// <summary>
    /// Gets a value indicating whether this is a chosen day rather than the last hours.
    /// </summary>
    public bool IsDay
        => Hours == 0;

    /// <summary>
    /// Gets the spans offered: the last hour, 3, 6, 12 and 24 hours, then a chosen day.
    /// </summary>
    /// <returns>The spans, in that order.</returns>
    public static IReadOnlyList<TimelineSpanChoice> All()
        => [
            .. AtcSoft.Video.Surveillance.Helpers.Activity.TimelineSpans.LastHoursChoices.Select(h => new TimelineSpanChoice(
                h,
                h == 1 ? Translations.LastHour : string.Format(CultureInfo.CurrentCulture, Translations.LastHours1, h))),
            new TimelineSpanChoice(0, Translations.ChooseADay),
        ];

    /// <inheritdoc />
    public override string ToString()
        => Label;
}