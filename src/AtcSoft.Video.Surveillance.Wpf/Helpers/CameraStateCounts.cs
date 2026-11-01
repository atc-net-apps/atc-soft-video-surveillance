namespace AtcSoft.Video.Surveillance.Wpf.Helpers;

/// <summary>
/// How many cameras are connected and how many record, counted from each
/// camera's own state. An event sets its camera's state, so a repeated or
/// missed event cannot make the counts drift; a load sets them all again.
/// </summary>
public sealed class CameraStateCounts
{
    private readonly Lock gate = new();
    private readonly Dictionary<Guid, bool> connected = new();
    private readonly Dictionary<Guid, bool> recording = new();

    /// <summary>
    /// Gets the number of connected cameras.
    /// </summary>
    public int Connected
    {
        get
        {
            lock (gate)
            {
                return connected.Values.Count(c => c);
            }
        }
    }

    /// <summary>
    /// Gets the number of cameras that record.
    /// </summary>
    public int Recording
    {
        get
        {
            lock (gate)
            {
                return recording.Values.Count(r => r);
            }
        }
    }

    /// <summary>
    /// Sets every camera's state from the server's camera list.
    /// </summary>
    public void Load(IEnumerable<Camera> cameras)
    {
        ArgumentNullException.ThrowIfNull(cameras);

        lock (gate)
        {
            connected.Clear();
            recording.Clear();
            foreach (var camera in cameras)
            {
                connected[camera.Id] = camera.ConnectionState == CameraConnectionState.Connected;
                recording[camera.Id] = camera.IsRecording;
            }
        }
    }

    public void Apply(ConnectionStateChangedEvent e)
    {
        ArgumentNullException.ThrowIfNull(e);

        lock (gate)
        {
            connected[e.CameraId] = e.NewState == CameraConnectionState.Connected;
        }
    }

    public void Apply(RecordingStateChangedEvent e)
    {
        ArgumentNullException.ThrowIfNull(e);

        lock (gate)
        {
            recording[e.CameraId] = HubStates.IsRecording(e.NewState);
        }
    }
}