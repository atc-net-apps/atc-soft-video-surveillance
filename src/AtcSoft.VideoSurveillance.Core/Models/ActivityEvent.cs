namespace AtcSoft.VideoSurveillance.Models;

/// <summary>
/// A stretch of a recording in which something moved (a confirmed track), as fractions of the
/// recording from its start (0) to its end (1).
/// </summary>
/// <param name="Start">Where it starts, 0 to 1.</param>
/// <param name="End">Where it ends, 0 to 1, after <paramref name="Start"/>.</param>
/// <param name="Zone">
/// The watch zone it happened in: the one in most of its stretches, the earlier on a tie;
/// <see langword="null"/> outside every watch zone. A timeline draws it in the zone's colour.
/// </param>
public sealed record ActivityEvent(
    double Start,
    double End,
    string? Zone = null);