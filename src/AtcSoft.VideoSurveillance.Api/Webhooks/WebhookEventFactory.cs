namespace AtcSoft.VideoSurveillance.Api.Webhooks;

/// <summary>
/// Turns what the server's services report into webhook events.
/// </summary>
public static class WebhookEventFactory
{
    /// <summary>
    /// A recording that starts or stops. A change from one kind of
    /// recording to another (plain, on motion, after motion) is not an
    /// event. Each file gives one start and one stop, so a recording that
    /// is split into segments reports every segment.
    /// </summary>
    /// <returns>The event, or <c>null</c> when nothing started or stopped.</returns>
    public static WebhookEvent? FromRecording(
        RecordingStateChangedEventArgs e,
        string cameraName,
        string? recordingRoot)
    {
        ArgumentNullException.ThrowIfNull(e);

        var wasRecording = e.OldState != RecordingState.Idle;
        var isRecording = e.NewState != RecordingState.Idle;
        if (wasRecording == isRecording)
        {
            return null;
        }

        return new WebhookEvent(
            isRecording ? WebhookEventTypes.RecordingStarted : WebhookEventTypes.RecordingStopped,
            e.CameraId,
            cameraName,
            new DateTimeOffset(DateTime.SpecifyKind(e.Timestamp, DateTimeKind.Utc)),
            new WebhookPayloadData(FilePath: ToRelativePath(e.FilePath, recordingRoot)));
    }

    /// <summary>
    /// A camera that connected or was lost. A camera that is still trying
    /// to connect is not an event.
    /// </summary>
    /// <returns>The event, or <c>null</c> for a state in between.</returns>
    public static WebhookEvent? FromConnection(
        Guid cameraId,
        string cameraName,
        ConnectionState state)
    {
        var type = state switch
        {
            ConnectionState.Connected => WebhookEventTypes.CameraConnected,
            ConnectionState.Disconnected or ConnectionState.Error => WebhookEventTypes.CameraDisconnected,
            _ => null,
        };

        return type is null
            ? null
            : new WebhookEvent(
                type,
                cameraId,
                cameraName,
                DateTimeOffset.UtcNow,
                new WebhookPayloadData(State: state.ToString()));
    }

    /// <summary>
    /// Motion that starts or stops.
    /// </summary>
    /// <returns>The event.</returns>
    public static WebhookEvent FromMotion(
        MotionDetectedEventArgs e,
        string cameraName)
    {
        ArgumentNullException.ThrowIfNull(e);

        return new WebhookEvent(
            e.IsMotionActive ? WebhookEventTypes.MotionStarted : WebhookEventTypes.MotionStopped,
            e.CameraId,
            cameraName,
            new DateTimeOffset(DateTime.SpecifyKind(e.Timestamp, DateTimeKind.Utc)),
            new WebhookPayloadData(ChangePercentage: e.ChangePercentage));
    }

    // The path the API lists the recording under: below the recording
    // folder, with forward slashes. The server's own folders are not sent out.
    private static string? ToRelativePath(
        string? filePath,
        string? recordingRoot)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            return null;
        }

        if (string.IsNullOrEmpty(recordingRoot) || !Path.IsPathRooted(filePath))
        {
            return Path.GetFileName(filePath);
        }

        var relative = Path.GetRelativePath(recordingRoot, filePath);
        return Path.IsPathRooted(relative) || relative.StartsWith("..", StringComparison.Ordinal)
            ? Path.GetFileName(filePath)
            : relative.Replace('\\', '/');
    }
}