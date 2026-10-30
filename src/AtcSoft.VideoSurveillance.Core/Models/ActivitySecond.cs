namespace AtcSoft.VideoSurveillance.Models;

/// <summary>
/// One analysed second of a recording's activity index.
/// </summary>
/// <param name="Offset">Whole seconds from the start of the index.</param>
/// <param name="Level">
/// The largest share of the picture that differed from the learnt scene in that second, 0 to 100.
/// Something that only sways (a tree) counts here too; it never becomes a track.
/// </param>
/// <param name="Tracks">The most confirmed moving things seen at once in that second.</param>
/// <param name="TrackIds">The ids of the confirmed tracks seen in that second, in rising order.</param>
public sealed record ActivitySecond(
    int Offset,
    double Level,
    int Tracks,
    IReadOnlyList<int> TrackIds)
{
    /// <summary>
    /// Gets the names of the watch zones a confirmed track was in during that second, in the order
    /// they were first seen; none outside every watch zone.
    /// </summary>
    public IReadOnlyList<string> Zones { get; init; } = [];
}