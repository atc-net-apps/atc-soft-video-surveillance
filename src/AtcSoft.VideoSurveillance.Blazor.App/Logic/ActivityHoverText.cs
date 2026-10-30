namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// The words the web player's activity timeline shows for the place under the mouse: the time,
/// how many things moved, and the watch zone they moved in. The same words as the desktop apps'.
/// </summary>
public static class ActivityHoverText
{
    /// <summary>
    /// How much of the timeline is looked at around the mouse, as a share of its width: as wide
    /// as the narrowest event band, so a widened band tells its event.
    /// </summary>
    public const double HalfWidth = ActivityTimelineLayout.MinimumEventWidth / 2 / ActivityTimelineShapes.BoxWidth;

    /// <summary>
    /// Gets the lines for a place on the timeline.
    /// </summary>
    /// <param name="hover">What happened there (<see cref="ActivityTimelineHover.At"/>).</param>
    /// <param name="recordingStart">When the recording started; the time into it when not known.</param>
    /// <param name="durationSeconds">How long the recording is, in seconds.</param>
    /// <returns>The lines, from the top.</returns>
    public static IReadOnlyList<string> Lines(
        ActivityHover hover,
        DateTimeOffset? recordingStart,
        double durationSeconds)
    {
        ArgumentNullException.ThrowIfNull(hover);

        var offset = TimeSpan.FromSeconds(Math.Max(hover.Position * durationSeconds, 0));
        var lines = new List<string>
        {
            recordingStart is { } start
                ? (start + offset).ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture)
                : offset.ToString(offset.TotalHours >= 1 ? @"hh\:mm\:ss" : @"mm\:ss", CultureInfo.InvariantCulture),
        };

        if (!hover.IsAnalysed)
        {
            lines.Add(Translations.ActivityHoverNotAnalysed);
        }
        else if (hover.Tracks > 0)
        {
            lines.Add(string.Format(CultureInfo.CurrentCulture, Translations.ActivityHoverMoving1, hover.Tracks));
        }
        else
        {
            lines.Add(Translations.ActivityHoverNothingMoving);
        }

        if (hover.Zone is { } zone)
        {
            lines.Add(string.Format(CultureInfo.CurrentCulture, Translations.ActivityHoverZone1, zone));
        }

        return lines;
    }

    /// <summary>
    /// Gets where the words stand over the timeline: the CSS left of their middle, kept clear of the edges.
    /// </summary>
    /// <param name="share">Where the mouse is on the timeline, 0 (left) to 1 (right).</param>
    public static string Left(double share)
        => SvgNumber.Format(Math.Clamp(share, 0.1, 0.9) * 100) + "%";
}