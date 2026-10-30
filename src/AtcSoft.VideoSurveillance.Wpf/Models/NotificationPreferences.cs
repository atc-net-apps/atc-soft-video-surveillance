namespace AtcSoft.VideoSurveillance.Wpf.Models;

/// <summary>
/// Client-side notification preferences persisted locally.
/// </summary>
public sealed class NotificationPreferences
{
    public bool NotifyOnDisconnect { get; set; } = true;

    public bool NotifyOnReconnect { get; set; }

    public bool NotifyOnMotionDetected { get; set; } = true;

    public bool NotifyOnRecordingStarted { get; set; }

    public bool NotifyOnRecordingStopped { get; set; }

    public bool PlaySound { get; set; }
}