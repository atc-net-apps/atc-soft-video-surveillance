namespace AtcSoft.VideoSurveillance.Wpf.Core.Helpers;

/// <summary>
/// The words a recording's activity timeline shows for the place under the mouse: the time, how
/// many things moved, and the watch zone they moved in.
/// </summary>
public static class ActivityHoverText
{
    /// <summary>
    /// Gets the lines for a place on the timeline.
    /// </summary>
    /// <param name="hover">What happened there (<see cref="ActivityTimelineHover.At"/>).</param>
    /// <param name="recordingStart">When the recording started, in local time; the time into it when not known.</param>
    /// <param name="durationSeconds">How long the recording is, in seconds.</param>
    /// <returns>The lines, from the top.</returns>
    public static IReadOnlyList<string> Lines(
        ActivityHover hover,
        DateTime? recordingStart,
        double durationSeconds)
    {
        ArgumentNullException.ThrowIfNull(hover);

        var offset = TimeSpan.FromSeconds(Math.Max(hover.Position * durationSeconds, 0));
        var lines = new List<string>
        {
            recordingStart is { } start
                ? (start + offset).ToString("HH:mm:ss", CultureInfo.InvariantCulture)
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
}