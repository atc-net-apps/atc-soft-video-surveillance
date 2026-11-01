namespace AtcSoft.Video.Surveillance.Wpf.ViewModels;

/// <summary>
/// A player that keeps the engine's rule: <see cref="Open"/> only from
/// <see cref="PlayerState.Stopped"/>, and only <see cref="Close"/> goes back to it.
/// A test makes it fail as the engine does when a stream ends.
/// </summary>
internal sealed class StrictVideoPlayer : IVideoPlayer
{
    private readonly Lock gate = new();
    private readonly List<Uri> opened = [];

    public event EventHandler<PlayerStateChangedEventArgs>? StateChanged;

    public PlayerState State { get; private set; } = PlayerState.Stopped;

    public VideoStreamInfo? StreamInfo => null;

    public double CurrentFps => 0;

    public long FramesDecoded => 0;

    public DateTime LastPacketUtc => DateTime.MinValue;

    public bool IsRecording => false;

    public IGpuAccelerator? GpuAccelerator => null;

    public int OpenFailures { get; private set; }

    public IReadOnlyList<Uri> Opened
    {
        get
        {
            lock (gate)
            {
                return [.. opened];
            }
        }
    }

    public void Open(
        Uri streamUri,
        StreamOptions? options = null)
    {
        lock (gate)
        {
            if (State != PlayerState.Stopped)
            {
                OpenFailures++;
                throw new InvalidOperationException($"Cannot open: player is in {State} state.");
            }

            opened.Add(streamUri);
        }

        SetState(PlayerState.Opening);
        SetState(PlayerState.Playing);
    }

    public void Close()
    {
        if (State == PlayerState.Stopped)
        {
            return;
        }

        SetState(PlayerState.Stopped);
    }

    /// <summary>
    /// Fails as the engine does at the end of a stream or a read timeout.
    /// </summary>
    public void Fail()
        => SetState(PlayerState.Error, "End of stream");

    public void StartRecording(string outputFilePath)
    {
    }

    public void StopRecording()
    {
    }

    public void SwitchRecording(string newOutputFilePath)
    {
    }

    public Task<byte[]?> CaptureFrameAsync(CancellationToken ct = default)
        => Task.FromResult<byte[]?>(null);

    public void SetRotation(VideoRotation rotation)
    {
    }

    public void Dispose()
    {
    }

    private void SetState(
        PlayerState newState,
        string? errorMessage = null)
    {
        var previous = State;
        State = newState;
        StateChanged?.Invoke(this, new PlayerStateChangedEventArgs(previous, newState, errorMessage));
    }
}