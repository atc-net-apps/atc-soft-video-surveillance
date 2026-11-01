namespace AtcSoft.Video.Surveillance.Wpf.Core.Services;

/// <summary>
/// The players of cameras that record while no tile shows them. A tile that goes away (another
/// layout is shown, or the camera is taken out of the layout) leaves a recording camera's player
/// and pipeline here instead of stopping the recording; the next tile for the camera takes them
/// back, still connected and recording.
/// </summary>
/// <remarks>
/// A parked player is let go (its recording stopped, the player closed) when its recording stops,
/// when its stream ends (nobody reconnects a camera no tile shows), when the camera is deleted,
/// and when the parking is disposed with the camera grid. Used on the UI thread; the player's and
/// the recording service's events are handled there through <c>post</c>.
/// </remarks>
public sealed class RecordingPlayerParking : IDisposable
{
    private readonly Dictionary<Guid, Parked> parked = [];
    private readonly Action<Action> post;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordingPlayerParking"/> class.
    /// </summary>
    /// <param name="post">Runs an action on the UI thread later; <see langword="null"/> uses the current dispatcher.</param>
    public RecordingPlayerParking(Action<Action>? post = null)
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        this.post = post ?? (action => _ = dispatcher.BeginInvoke(action));
    }

    /// <summary>
    /// Gets how many players are parked.
    /// </summary>
    public int Count => parked.Count;

    /// <summary>
    /// Gets a value indicating whether a camera's player is parked.
    /// </summary>
    /// <param name="cameraId">The camera.</param>
    public bool IsParked(Guid cameraId)
        => parked.ContainsKey(cameraId);

    /// <summary>
    /// Parks a recording camera's player and pipeline. A player already parked for the camera is let go.
    /// </summary>
    /// <param name="cameraId">The camera.</param>
    /// <param name="player">The player, connected and recording.</param>
    /// <param name="pipeline">The pipeline the recording runs on.</param>
    /// <param name="recordingService">The recording service that records the camera.</param>
    public void Park(
        Guid cameraId,
        IVideoPlayer player,
        IMediaPipeline? pipeline,
        IRecordingService recordingService)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(recordingService);
        ObjectDisposedException.ThrowIf(disposed, this);

        Release(cameraId);

        var entry = new Parked(cameraId, player, pipeline, recordingService, this);
        parked[cameraId] = entry;
        player.StateChanged += entry.OnPlayerStateChanged;
        recordingService.RecordingStateChanged += entry.OnRecordingStateChanged;
    }

    /// <summary>
    /// Takes a camera's parked player back, still connected and recording.
    /// </summary>
    /// <param name="cameraId">The camera.</param>
    /// <param name="player">The player.</param>
    /// <param name="pipeline">The pipeline the recording runs on.</param>
    /// <returns><see langword="true"/> when the camera had a parked player.</returns>
    public bool TryTake(
        Guid cameraId,
        [NotNullWhen(true)] out IVideoPlayer? player,
        out IMediaPipeline? pipeline)
    {
        if (!parked.Remove(cameraId, out var entry))
        {
            player = null;
            pipeline = null;
            return false;
        }

        entry.Unsubscribe();
        player = entry.Player;
        pipeline = entry.Pipeline;
        return true;
    }

    /// <summary>
    /// Lets a camera's parked player go: its recording is stopped and the player closed.
    /// </summary>
    /// <param name="cameraId">The camera.</param>
    public void Release(Guid cameraId)
    {
        if (!parked.Remove(cameraId, out var entry))
        {
            return;
        }

        entry.Unsubscribe();
        entry.RecordingService.StopRecording(cameraId);
        entry.Pipeline?.Dispose();
        entry.Player.Dispose();
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        foreach (var cameraId in parked.Keys.ToList())
        {
            Release(cameraId);
        }
    }

    // Looked at again on the UI thread: a segment switch reports idle and recording at once, and a
    // player taken back meanwhile is no longer this parking's.
    internal void Recheck(Parked entry)
    {
        if (!parked.TryGetValue(entry.CameraId, out var current) || !ReferenceEquals(current, entry))
        {
            return;
        }

        var streamEnded = entry.Player.State is PlayerState.Error or PlayerState.Stopped;
        if (streamEnded || !entry.RecordingService.IsRecording(entry.CameraId))
        {
            Release(entry.CameraId);
        }
    }

    internal sealed class Parked(
        Guid cameraId,
        IVideoPlayer player,
        IMediaPipeline? pipeline,
        IRecordingService recordingService,
        RecordingPlayerParking parking)
    {
        public Guid CameraId { get; } = cameraId;

        public IVideoPlayer Player { get; } = player;

        public IMediaPipeline? Pipeline { get; } = pipeline;

        public IRecordingService RecordingService { get; } = recordingService;

        public void OnPlayerStateChanged(
            object? sender,
            PlayerStateChangedEventArgs e)
        {
            if (e.NewState is PlayerState.Error or PlayerState.Stopped)
            {
                parking.post(() => parking.Recheck(this));
            }
        }

        public void OnRecordingStateChanged(
            object? sender,
            RecordingStateChangedEventArgs e)
        {
            if (e.CameraId == CameraId)
            {
                parking.post(() => parking.Recheck(this));
            }
        }

        public void Unsubscribe()
        {
            Player.StateChanged -= OnPlayerStateChanged;
            RecordingService.RecordingStateChanged -= OnRecordingStateChanged;
        }
    }
}