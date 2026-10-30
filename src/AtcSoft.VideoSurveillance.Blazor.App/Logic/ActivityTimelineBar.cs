namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// One bar of a recording's activity timeline as drawn: the stretches under one column, with the
/// most that happened in them. Its place is a fraction of what the timeline shows, 0 to 1.
/// </summary>
/// <param name="Start">Where it starts on the timeline, 0 to 1.</param>
/// <param name="End">Where it ends on the timeline, 0 to 1.</param>
/// <param name="Level">The most that changed in its stretches, 0 to 100.</param>
/// <param name="IsAnalysed">Whether any of its stretches was analysed; drawn dim when none was.</param>
public sealed record ActivityTimelineBar(
    double Start,
    double End,
    double Level,
    bool IsAnalysed);