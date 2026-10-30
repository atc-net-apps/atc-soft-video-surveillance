namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// How the Timeline page shows why a lane has a gap: a colour and a text per reason. The colours
/// are a copy of Core's <c>TimelineGapColors</c>, kept equal by a test. A reason this client does
/// not know yet looks like nothing recorded.
/// </summary>
public static class TimelineGapStyle
{
    /// <summary>
    /// The reasons in the order the legend lists them.
    /// </summary>
    public static IReadOnlyList<string> Reasons { get; } =
    [
        TimelineGapReason.Names.Offline,
        TimelineGapReason.Names.DiskFull,
        TimelineGapReason.Names.ServerDown,
        TimelineGapReason.Names.NotRecording,
        TimelineGapReason.Names.Unknown,
    ];

    /// <summary>
    /// Gets the colour of a reason, as SVG and CSS take it.
    /// </summary>
    /// <param name="reason">The reason's wire value.</param>
    public static string Fill(string? reason)
        => reason switch
        {
            TimelineGapReason.Names.Offline => "rgba(229,57,53,0.35)",
            TimelineGapReason.Names.DiskFull => "rgba(255,179,0,0.45)",
            TimelineGapReason.Names.ServerDown => "rgba(126,87,194,0.35)",
            TimelineGapReason.Names.NotRecording => "rgba(128,128,128,0.35)",
            _ => "rgba(128,128,128,0.18)",
        };

    /// <summary>
    /// Gets the style of a reason's swatch in the legend.
    /// </summary>
    /// <param name="reason">The reason's wire value.</param>
    public static string Swatch(string? reason)
        => $"display: inline-block; width: 14px; height: 14px; background: {Fill(reason)};";

    /// <summary>
    /// Gets the text of a reason, in the viewer's language.
    /// </summary>
    /// <param name="reason">The reason's wire value.</param>
    public static string Text(string? reason)
        => reason switch
        {
            TimelineGapReason.Names.Offline => Translations.GapOffline,
            TimelineGapReason.Names.DiskFull => Translations.GapDiskFull,
            TimelineGapReason.Names.ServerDown => Translations.GapServerDown,
            TimelineGapReason.Names.NotRecording => Translations.GapNotRecording,
            _ => Translations.GapUnknown,
        };
}