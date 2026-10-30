namespace AtcSoft.Video.Surveillance.Wpf.Models;

/// <summary>
/// Types of notification events raised by the surveillance hub.
/// </summary>
public enum NotificationEventType
{
    CameraDisconnected,
    CameraReconnected,
    MotionDetected,
    RecordingStarted,
    RecordingStopped,
}