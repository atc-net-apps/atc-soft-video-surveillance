namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// A gap of a lane on the Timeline page: where it is drawn, its colour, and the reason shown when
/// the pointer rests on it.
/// </summary>
/// <param name="Rect">Where, in the lane's box.</param>
/// <param name="Fill">The colour, as SVG takes it.</param>
/// <param name="Text">Why nothing was recorded, in the viewer's language.</param>
public sealed record TimelineGapShape(
    TimelineRect Rect,
    string Fill,
    string Text);