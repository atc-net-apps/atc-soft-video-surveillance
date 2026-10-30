namespace AtcSoft.VideoSurveillance.Api.Domain.Events;

/// <summary>
/// An event of the feed with its sequence, which is its id on the stream.
/// </summary>
public readonly record struct SequencedEvent(
    long Sequence,
    SurveillanceEvent Event);