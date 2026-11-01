namespace AtcSoft.Video.Surveillance.Wpf.Models;

/// <summary>
/// A single notification entry for the history log.
/// </summary>
public sealed record NotificationEntry(
    DateTimeOffset Timestamp,
    string Title,
    string Message,
    NotificationEventType EventType)
{
    /// <summary>
    /// Gets the kind of notification in the chosen language.
    /// </summary>
    public string TypeText
        => NotificationEventTypeText.For(EventType);
}