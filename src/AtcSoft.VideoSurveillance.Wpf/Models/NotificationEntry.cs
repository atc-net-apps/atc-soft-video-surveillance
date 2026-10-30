namespace AtcSoft.VideoSurveillance.Wpf.Models;

/// <summary>
/// A single notification entry for the history log.
/// </summary>
public sealed record NotificationEntry(
    DateTimeOffset Timestamp,
    string Title,
    string Message,
    NotificationEventType EventType);