using CoreConnectionState = AtcSoft.VideoSurveillance.Enums.ConnectionState;
using CoreUsbCameraLifecyclePhase = AtcSoft.VideoSurveillance.Enums.UsbCameraLifecyclePhase;

namespace AtcSoft.VideoSurveillance.Api.Domain.Mapping;

/// <summary>
/// Turns the server's events into the hub's events, as the specification
/// declares them: the same models and values as the REST API.
/// </summary>
public static class HubEventMapping
{
    public static ConnectionStateChangedEvent ToHubEvent(
        Guid cameraId,
        CoreConnectionState state,
        DateTimeOffset timestamp)
        => new(
            CameraId: cameraId,
            NewState: state.ToApiConnectionState(),
            Timestamp: timestamp);

    public static RecordingStateChangedEvent ToHubEvent(
        this RecordingStateChangedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        return new RecordingStateChangedEvent(
            CameraId: e.CameraId,
            NewState: e.NewState.ToApiRecordingState(),
            OldState: e.OldState.ToApiRecordingState(),
            FilePath: e.FilePath,
            Timestamp: new DateTimeOffset(e.Timestamp, TimeSpan.Zero));
    }

    public static MotionDetectedEvent ToHubEvent(this MotionDetectedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        return new MotionDetectedEvent(
            CameraId: e.CameraId,
            IsMotionActive: e.IsMotionActive,
            ChangePercentage: e.ChangePercentage,
            BoundingBoxes: [.. e.BoundingBoxes.Select(b => new MotionBox(X: b.X, Y: b.Y, Width: b.Width, Height: b.Height, TrackId: b.TrackId))],
            AnalysisWidth: e.AnalysisWidth,
            AnalysisHeight: e.AnalysisHeight,
            Timestamp: new DateTimeOffset(e.Timestamp, TimeSpan.Zero));
    }

    public static UsbCameraLifecycleEvent ToHubEvent(
        this UsbCameraLifecycleChangedEventArgs e,
        DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(e);

        var phase = e.Phase == CoreUsbCameraLifecyclePhase.Replugged
            ? UsbCameraLifecyclePhase.Replugged
            : UsbCameraLifecyclePhase.Unplugged;

        return new UsbCameraLifecycleEvent(
            CameraId: e.CameraId,
            Phase: phase,
            DeviceId: e.Device.DeviceId,
            FriendlyName: e.Device.FriendlyName,
            Timestamp: timestamp);
    }

    public static SurveillanceEvent ToStreamEvent(
        this ConnectionStateChangedEvent e)
    {
        ArgumentNullException.ThrowIfNull(e);

        return new SurveillanceEvent(
            Type: StreamEventTypes.ConnectionStateChanged,
            CameraId: e.CameraId,
            Timestamp: e.Timestamp,
            Connection: e);
    }

    public static SurveillanceEvent ToStreamEvent(
        this RecordingStateChangedEvent e)
    {
        ArgumentNullException.ThrowIfNull(e);

        return new SurveillanceEvent(
            Type: StreamEventTypes.RecordingStateChanged,
            CameraId: e.CameraId,
            Timestamp: e.Timestamp,
            Recording: e);
    }

    public static SurveillanceEvent ToStreamEvent(this MotionDetectedEvent e)
    {
        ArgumentNullException.ThrowIfNull(e);

        return new SurveillanceEvent(
            Type: StreamEventTypes.MotionDetected,
            CameraId: e.CameraId,
            Timestamp: e.Timestamp,
            Motion: e);
    }

    public static SurveillanceEvent ToStreamEvent(this ActivityChangedEvent e)
    {
        ArgumentNullException.ThrowIfNull(e);

        return new SurveillanceEvent(
            Type: StreamEventTypes.ActivityChanged,
            CameraId: e.CameraId,
            Timestamp: e.End ?? e.Start,
            Activity: e);
    }

    public static SurveillanceEvent ToStreamEvent(
        this UsbCameraLifecycleEvent e)
    {
        ArgumentNullException.ThrowIfNull(e);

        return new SurveillanceEvent(
            Type: StreamEventTypes.UsbCameraLifecycleChanged,
            CameraId: e.CameraId,
            Timestamp: e.Timestamp,
            Usb: e);
    }

    /// <summary>
    /// Maps a change of a recorded camera's activity to its event, with the recording named the
    /// way the listing names it.
    /// </summary>
    /// <param name="e">The change.</param>
    /// <param name="recordingPath">The recordings root.</param>
    public static ActivityChangedEvent ToEvent(
        this AtcSoft.VideoSurveillance.Events.CameraActivityChangedEventArgs e,
        string recordingPath)
    {
        ArgumentNullException.ThrowIfNull(e);

        return new ActivityChangedEvent(
            CameraId: e.CameraId,
            IsActive: e.IsActive,
            Start: e.Start,
            FilePath: RecordingCatalog.ToRelativePath(e.VideoPath, recordingPath),
            OffsetSeconds: e.OffsetSeconds,
            PeakLevel: e.PeakLevel,
            Tracks: e.Tracks,
            End: e.End);
    }
}