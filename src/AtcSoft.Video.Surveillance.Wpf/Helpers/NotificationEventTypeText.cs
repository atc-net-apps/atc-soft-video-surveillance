namespace AtcSoft.Video.Surveillance.Wpf.Helpers;

/// <summary>
/// The words for a kind of notification, in the chosen language: the history's type column and its filter chips.
/// </summary>
public static class NotificationEventTypeText
{
    /// <summary>
    /// Gets the kinds of notification in the order the filter shows them.
    /// </summary>
    public static IReadOnlyList<NotificationEventType> All { get; } =
    [
        NotificationEventType.MotionDetected,
        NotificationEventType.RecordingStarted,
        NotificationEventType.RecordingStopped,
        NotificationEventType.CameraDisconnected,
        NotificationEventType.CameraReconnected,
    ];

    /// <summary>
    /// Gets the words for a kind of notification.
    /// </summary>
    /// <param name="eventType">The kind of notification.</param>
    public static string For(NotificationEventType eventType)
        => eventType switch
        {
            NotificationEventType.CameraDisconnected => Translations.CameraDisconnectedNotification,
            NotificationEventType.CameraReconnected => Translations.CameraConnectedNotification,
            NotificationEventType.MotionDetected => Translations.Motion,
            NotificationEventType.RecordingStarted => Translations.RecordingStartedNotification,
            NotificationEventType.RecordingStopped => Translations.RecordingStoppedNotification,
            _ => eventType.ToString(),
        };
}