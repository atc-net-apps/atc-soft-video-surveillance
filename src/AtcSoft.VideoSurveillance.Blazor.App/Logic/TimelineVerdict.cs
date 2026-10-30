namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// A verdict the web player's timeline offers for a stretch of a recording (a copy of Core's
/// <c>TimelineVerdict</c>).
/// </summary>
/// <param name="Verdict">One of <see cref="TimelineVerdicts"/>' verdicts, as the server names them.</param>
/// <param name="FromSeconds">Where the stretch starts, in seconds into the recording.</param>
/// <param name="ToSeconds">Where it ends.</param>
public sealed record TimelineVerdict(
    string Verdict,
    double FromSeconds,
    double ToSeconds);