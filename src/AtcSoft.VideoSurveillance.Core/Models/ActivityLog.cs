namespace AtcSoft.VideoSurveillance.Models;

/// <summary>
/// The activity index of one recording: what moved when, second by second.
/// </summary>
/// <param name="Start">When the index starts, in UTC. Offsets count from here.</param>
/// <param name="Seconds">The analysed seconds, in rising order. A second without an entry was not analysed.</param>
public sealed record ActivityLog(
    DateTimeOffset Start,
    IReadOnlyList<ActivitySecond> Seconds)
{
    /// <summary>
    /// Gets how many seconds the index covers: up to the end of its last analysed second.
    /// </summary>
    public int DurationSeconds
        => Seconds.Count == 0 ? 0 : Seconds[^1].Offset + 1;
}