namespace AtcSoft.VideoSurveillance.Models;

/// <summary>
/// One camera's timeline over a span: where it was recorded, the stretches with the most that
/// happened in each, and the events.
/// </summary>
/// <param name="Coverage">Where recordings cover the span, from its start.</param>
/// <param name="Stretches">The span in equal stretches, from its start.</param>
/// <param name="Events">What moved in the span, from its start.</param>
public sealed record CameraTimeline(
    IReadOnlyList<CoverageSpan> Coverage,
    IReadOnlyList<TimelineStretch> Stretches,
    IReadOnlyList<TimelineActivityEvent> Events);