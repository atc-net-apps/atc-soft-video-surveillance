namespace AtcSoft.Video.Surveillance.Wpf.Core.Services;

/// <summary>
/// The players of cameras that record while no tile shows them. A tile that goes away (another
/// layout is shown, or the camera is taken out of the layout) leaves a recording camera's player
/// and pipeline here instead of stopping the recording; the next tile for the camera takes them
/// back, still connected and recording.
/// </summary>
/// <remarks>
/// A parked camera whose stream ends (a network blip, a camera restart, a stream that went silent)
/// is opened again, with back-off, and its recording goes on in a new file once pictures arrive.
/// A parked player is let go (its recording stopped, the player closed) when its recording stops
/// for good, when the camera is deleted, and when the parking is disposed with the camera grid.
/// Used on the UI thread; the player's and the recording service's events are handled there
/// through <c>post</c>.
/// </remarks>
public sealed class RecordingPlayerParking : IDisposable
{
    /// <summary>
    /// The wait before the first new attempt to open a parked camera's stream; it doubles with each
    /// failed attempt, up to <see cref="ReopenMaxDelay"/>.
    /// </summary>
    public static readonly TimeSpan ReopenFirstDelay = TimeSpan.FromSeconds(5);

    /// <summary>
    /// The longest wait between attempts to open a parked camera's stream.
    /// </summary>
    public static readonly TimeSpan ReopenMaxDelay = TimeSpan.FromSeconds(60);

    private readonly Dictionary<Guid, Parked> parked = [];
    private readonly Action<Action> post;
    private readonly Func<TimeSpan, Action, Action> schedule;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordingPlayerParking"/> class.
    /// </summary>
    /// <param name="post">Runs an action on the UI thread later; <see langword="null"/> uses the current dispatcher.</param>
    /// <param name="schedule">
    /// Runs an action on the UI thread after a wait and returns what cancels it; <see langword="null"/>
    /// uses a timer on the current dispatcher.
    /// </param>
    public RecordingPlayerParking(
        Action<Action>? post = null,
        Func<TimeSpan, Action, Action>? schedule = null)
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        this.post = post ?? (action => _ = dispatcher.BeginInvoke(action));
        this.schedule = schedule ?? ((delay, action) => ScheduleOn(dispatcher, delay, action));
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
    /// <param name="reopen">
    /// Opens the camera's stream on the player again (blocking; run in the background) when it
    /// ended. <see langword="null"/> lets the player go when its stream ends.
    /// </param>
    public void Park(
        Guid cameraId,
        IVideoPlayer player,
        IMediaPipeline? pipeline,
        IRecordingService recordingService,
        Action<IVideoPlayer>? reopen = null)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(recordingService);
        ObjectDisposedException.ThrowIf(disposed, this);

        Release(cameraId);

        var entry = new Parked(cameraId, player, pipeline, recordingService, reopen, this);
        parked[cameraId] = entry;
        player.StateChanged += entry.OnPlayerStateChanged;
        recordingService.RecordingStateChanged += entry.OnRecordingStateChanged;

        // A stream that ended just before the tile went away is opened again from here.
        if (player.State is PlayerState.Error or PlayerState.Stopped)
        {
            post(() => Recheck(entry));
        }
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

    /// <summary>
    /// Gets the wait before a new attempt to open a stream, after a number of failed attempts.
    /// </summary>
    /// <param name="failedAttempts">The attempts that failed in a row.</param>
    /// <returns>The wait.</returns>
    public static TimeSpan ReopenDelay(int failedAttempts)
    {
        var factor = Math.Pow(2, Math.Clamp(failedAttempts, 0, 16));
        var delay = TimeSpan.FromTicks((long)Math.Min(ReopenFirstDelay.Ticks * factor, ReopenMaxDelay.Ticks));
        return delay;
    }

    // Looked at again on the UI thread: a segment switch reports idle and recording at once, and a
    // player taken back meanwhile is no longer this parking's.
    internal void Recheck(Parked entry)
    {
        if (!IsCurrent(entry))
        {
            return;
        }

        if (!entry.RecordingService.IsRecordingOrResuming(entry.CameraId))
        {
            Release(entry.CameraId);
            return;
        }

        if (entry.IsOpening)
        {
            return;
        }

        switch (entry.Player.State)
        {
            case PlayerState.Playing:
                entry.FailedAttempts = 0;

                // The file ended with the stream; the recording goes on in a new one.
                entry.RecordingService.ResumeRecording(entry.CameraId);
                break;
            case PlayerState.Error:
            case PlayerState.Stopped:
                ScheduleReopen(entry);
                break;
        }
    }

    private static Action ScheduleOn(
        Dispatcher dispatcher,
        TimeSpan delay,
        Action action)
    {
        var timer = new DispatcherTimer(DispatcherPriority.Normal, dispatcher) { Interval = delay };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            action();
        };
        timer.Start();
        return timer.Stop;
    }

    private bool IsCurrent(Parked entry)
        => parked.TryGetValue(entry.CameraId, out var current) && ReferenceEquals(current, entry);

    private void ScheduleReopen(Parked entry)
    {
        if (entry.Reopen is null)
        {
            // Nobody can open it again.
            Release(entry.CameraId);
            return;
        }

        if (entry.CancelReopen is not null)
        {
            return;
        }

        var delay = ReopenDelay(entry.FailedAttempts);
        entry.FailedAttempts++;
        entry.CancelReopen = schedule(delay, () =>
        {
            entry.CancelReopen = null;
            Reopen(entry);
        });
    }

    private void Reopen(Parked entry)
    {
        if (!IsCurrent(entry) ||
            entry.IsOpening ||
            entry.Player.State is not (PlayerState.Error or PlayerState.Stopped))
        {
            return;
        }

        entry.IsOpening = true;
        var player = entry.Player;
        var reopen = entry.Reopen!;
        _ = Task.Run(() =>
        {
            try
            {
                player.Close();
                reopen(player);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // The state tells: the next look tries again later.
            }
            finally
            {
                post(() =>
                {
                    entry.IsOpening = false;
                    Recheck(entry);
                });
            }
        });
    }

    internal sealed class Parked(
        Guid cameraId,
        IVideoPlayer player,
        IMediaPipeline? pipeline,
        IRecordingService recordingService,
        Action<IVideoPlayer>? reopen,
        RecordingPlayerParking parking)
    {
        public Guid CameraId { get; } = cameraId;

        public IVideoPlayer Player { get; } = player;

        public IMediaPipeline? Pipeline { get; } = pipeline;

        public IRecordingService RecordingService { get; } = recordingService;

        public Action<IVideoPlayer>? Reopen { get; } = reopen;

        public int FailedAttempts { get; set; }

        public bool IsOpening { get; set; }

        public Action? CancelReopen { get; set; }

        public void OnPlayerStateChanged(
            object? sender,
            PlayerStateChangedEventArgs e)
        {
            if (e.NewState is PlayerState.Error or PlayerState.Stopped or PlayerState.Playing)
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
            CancelReopen?.Invoke();
            CancelReopen = null;
            Player.StateChanged -= OnPlayerStateChanged;
            RecordingService.RecordingStateChanged -= OnRecordingStateChanged;
        }
    }
}