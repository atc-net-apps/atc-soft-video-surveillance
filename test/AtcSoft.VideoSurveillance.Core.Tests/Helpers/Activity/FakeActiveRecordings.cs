namespace AtcSoft.VideoSurveillance.Helpers.Activity;

/// <summary>
/// The recordings of an app, started, segmented and stopped by the test.
/// </summary>
public sealed class FakeActiveRecordings : IActiveRecordings
{
    private readonly Dictionary<Guid, RecordingSession> sessions = [];
    private readonly Dictionary<Guid, IMediaPipeline> pipelines = [];

    public event EventHandler<RecordingStateChangedEventArgs>? RecordingStateChanged;

    public RecordingSession? GetSession(Guid cameraId)
        => sessions.GetValueOrDefault(cameraId);

    public IMediaPipeline? FindPipeline(Guid cameraId)
        => pipelines.GetValueOrDefault(cameraId);

    public bool IsRecording(Guid cameraId)
        => sessions.ContainsKey(cameraId);

    public IReadOnlyList<RecordingSession> GetActiveSessions()
        => [.. sessions.Values];

    public void Start(
        Guid cameraId,
        string filePath,
        IMediaPipeline pipeline,
        bool raise = true)
    {
        sessions[cameraId] = new RecordingSession(cameraId, "Garden", filePath, isManualRecording: true);
        pipelines[cameraId] = pipeline;
        if (raise)
        {
            ChangeState(cameraId, RecordingState.Idle, RecordingState.Recording, filePath);
        }
    }

    public void Segment(
        Guid cameraId,
        string newFilePath)
    {
        var old = sessions[cameraId].CurrentFilePath;
        sessions[cameraId] = new RecordingSession(cameraId, "Garden", newFilePath, isManualRecording: true);
        ChangeState(cameraId, RecordingState.Recording, RecordingState.Idle, old);
        ChangeState(cameraId, RecordingState.Idle, RecordingState.Recording, newFilePath);
    }

    public void Stop(Guid cameraId)
    {
        var file = sessions[cameraId].CurrentFilePath;
        sessions.Remove(cameraId);
        pipelines.Remove(cameraId);
        ChangeState(cameraId, RecordingState.Recording, RecordingState.Idle, file);
    }

    public void ChangeState(
        Guid cameraId,
        RecordingState oldState,
        RecordingState newState,
        string? filePath)
        => RecordingStateChanged?.Invoke(this, new RecordingStateChangedEventArgs(cameraId, oldState, newState, filePath));
}