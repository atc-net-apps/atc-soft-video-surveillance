namespace AtcSoft.VideoSurveillance.Wpf.Core.UserControls;

/// <summary>
/// A wish to zoom an <see cref="ActivityTimeline"/>, given to its <see cref="ActivityTimeline.ZoomCommand"/>.
/// </summary>
/// <param name="Focus">The place in the recording that should keep its spot, 0 to 1: where the mouse is.</param>
/// <param name="ZoomIn">Whether to zoom in; out when <see langword="false"/>.</param>
public sealed record ActivityTimelineZoomRequest(
    double Focus,
    bool ZoomIn);