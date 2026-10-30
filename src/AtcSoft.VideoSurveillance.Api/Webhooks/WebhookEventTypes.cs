namespace AtcSoft.VideoSurveillance.Api.Webhooks;

/// <summary>
/// The names of the events a webhook can receive.
/// </summary>
public static class WebhookEventTypes
{
    public const string CameraConnected = "camera.connected";

    public const string CameraDisconnected = "camera.disconnected";

    public const string RecordingStarted = "recording.started";

    public const string RecordingStopped = "recording.stopped";

    public const string MotionStarted = "motion.started";

    public const string MotionStopped = "motion.stopped";

    /// <summary>Sent on request to one receiver, to check that it can be reached.</summary>
    public const string Test = "webhook.test";
}