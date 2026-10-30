namespace AtcSoft.VideoSurveillance.Storybook.Scoring;

/// <summary>
/// How well one object was found.
/// </summary>
/// <param name="Id">The object.</param>
/// <param name="MustBoxFrames">The frames it had to have a box in (after the latency allowance).</param>
/// <param name="HitFrames">The frames it had one.</param>
/// <param name="LatencySeconds">How long after it started to move it first had a box; <see langword="null"/> when never.</param>
/// <param name="IdSwitches">How often its track id changed; <see langword="null"/> without track ids.</param>
public sealed record ObjectScore(
    string Id,
    int MustBoxFrames,
    int HitFrames,
    double? LatencySeconds,
    int? IdSwitches)
{
    public double Recall
        => MustBoxFrames == 0 ? 1 : HitFrames / (double)MustBoxFrames;
}