namespace AtcSoft.VideoSurveillance.Wpf.Core.UserControls;

/// <summary>
/// How a timeline shows why a camera has a gap: a colour (from <see cref="TimelineGapColors"/>, the
/// same in every client) and a text per reason. The brushes are public for a legend in XAML.
/// </summary>
public static class TimelineGapLook
{
    public static Brush OfflineBrush { get; } = Frozen(GapReason.Offline);

    public static Brush DiskFullBrush { get; } = Frozen(GapReason.DiskFull);

    public static Brush ServerDownBrush { get; } = Frozen(GapReason.ServerDown);

    public static Brush NotRecordingBrush { get; } = Frozen(GapReason.NotRecording);

    public static Brush UnknownBrush { get; } = Frozen(GapReason.Unknown);

    /// <summary>
    /// Gets the brush of a reason.
    /// </summary>
    /// <param name="reason">Why nothing was recorded.</param>
    public static Brush BrushOf(GapReason reason)
        => reason switch
        {
            GapReason.Offline => OfflineBrush,
            GapReason.DiskFull => DiskFullBrush,
            GapReason.ServerDown => ServerDownBrush,
            GapReason.NotRecording => NotRecordingBrush,
            _ => UnknownBrush,
        };

    /// <summary>
    /// Gets the text of a reason, in the viewer's language.
    /// </summary>
    /// <param name="reason">Why nothing was recorded.</param>
    public static string TextOf(GapReason reason)
        => reason switch
        {
            GapReason.Offline => Translations.GapOffline,
            GapReason.DiskFull => Translations.GapDiskFull,
            GapReason.ServerDown => Translations.GapServerDown,
            GapReason.NotRecording => Translations.GapNotRecording,
            _ => Translations.GapUnknown,
        };

    private static SolidColorBrush Frozen(GapReason reason)
    {
        var (a, r, g, b) = TimelineGapColors.Of(reason);
        var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
        brush.Freeze();
        return brush;
    }
}