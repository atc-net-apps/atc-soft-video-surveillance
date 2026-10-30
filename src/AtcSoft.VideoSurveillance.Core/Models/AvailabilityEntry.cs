namespace AtcSoft.VideoSurveillance.Models;

/// <summary>
/// One change in whether a camera could be recorded.
/// </summary>
/// <param name="Time">When it happened, in UTC.</param>
/// <param name="Change">What happened.</param>
public sealed record AvailabilityEntry(
    DateTimeOffset Time,
    AvailabilityChange Change);