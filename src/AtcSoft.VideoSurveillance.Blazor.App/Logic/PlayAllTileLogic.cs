namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// One camera of *play all from here* on the web: its recordings from the chosen moment, played
/// one after another as the pacer's clock reaches each, and what the tile says when it shows no
/// picture. A recording counts as playing only once its picture is near where it was asked to
/// start, and one that fails is skipped. It does not touch the browser: it answers with the commands
/// for the video element.
/// </summary>
public sealed class PlayAllTileLogic
{
    private readonly IReadOnlyList<PlayAllSegment> segments;
    private int index;
    private bool isPaused;
    private bool isOpened;
    private double requestedSeconds;

    /// <summary>
    /// Initializes a new instance of the <see cref="PlayAllTileLogic"/> class.
    /// </summary>
    /// <param name="cameraName">The camera's name, shown on the tile.</param>
    /// <param name="segments">Its recordings that end after the chosen moment, in time order.</param>
    public PlayAllTileLogic(
        string cameraName,
        IReadOnlyList<PlayAllSegment> segments)
    {
        ArgumentNullException.ThrowIfNull(segments);

        CameraName = cameraName;
        this.segments = segments;
        MoveTo(0);
    }

    public string CameraName { get; }

    public PlayAllTileState State { get; private set; }

    /// <summary>
    /// Gets what the tile says over its picture: empty while it plays.
    /// </summary>
    public string StatusText { get; private set; } = string.Empty;

    private PlayAllSegment? Current
        => index < segments.Count ? segments[index] : null;

    /// <summary>
    /// Tells the pacer where the tile is.
    /// </summary>
    /// <param name="currentSeconds">How far into the recording the video element is.</param>
    /// <returns>The tile as the pacer reads it.</returns>
    public PlayAllTile Report(double currentSeconds)
    {
        if (State == PlayAllTileState.Loading && isOpened && PlayAllStart.IsNear(TimeSpan.FromSeconds(currentSeconds), TimeSpan.FromSeconds(requestedSeconds)))
        {
            State = PlayAllTileState.Playing;
            StatusText = string.Empty;
        }

        return State switch
        {
            PlayAllTileState.Playing or PlayAllTileState.Held when Current is { } segment
                => new PlayAllTile(State, At: segment.RecordingStart + TimeSpan.FromSeconds(currentSeconds)),
            PlayAllTileState.Waiting when Current is { } segment
                => new PlayAllTile(State, NextStart: segment.Start),
            _ => new PlayAllTile(State),
        };
    }

    /// <summary>
    /// Does what the pacer asks.
    /// </summary>
    /// <param name="action">The pacer's action for this tile.</param>
    /// <param name="clock">The pacer's clock.</param>
    /// <returns>The command for the video element, or <see langword="null"/>.</returns>
    public PlayAllCommand? Apply(
        PlayAllAction action,
        DateTimeOffset clock)
    {
        switch (action)
        {
            case PlayAllAction.Start when Current is { } segment:
                requestedSeconds = Math.Max((clock - segment.RecordingStart).TotalSeconds, 0);
                isOpened = false;
                State = PlayAllTileState.Loading;
                StatusText = Translations.Loading;
                return new PlayAllCommand(PlayAllCommandKind.Open, segment.Source, requestedSeconds);
            case PlayAllAction.Hold when State == PlayAllTileState.Playing:
                State = PlayAllTileState.Held;
                return new PlayAllCommand(PlayAllCommandKind.Pause);
            case PlayAllAction.Resume when State == PlayAllTileState.Held:
                State = PlayAllTileState.Playing;
                return isPaused ? null : new PlayAllCommand(PlayAllCommandKind.Play);
            case PlayAllAction.GiveUp when State is PlayAllTileState.Loading or PlayAllTileState.Playing:
                Fail();
                return new PlayAllCommand(PlayAllCommandKind.Close);
            default:
                return null;
        }
    }

    /// <summary>
    /// The video element began to play the opened recording. It counts as playing once it is near
    /// where it was asked to start.
    /// </summary>
    /// <returns>A pause when everything is paused, else <see langword="null"/>.</returns>
    public PlayAllCommand? Opened()
    {
        if (State != PlayAllTileState.Loading)
        {
            return null;
        }

        isOpened = true;
        return isPaused ? new PlayAllCommand(PlayAllCommandKind.Pause) : null;
    }

    /// <summary>
    /// The recording played to its end: the tile waits for its next one.
    /// </summary>
    public void Ended()
        => MoveTo(index + 1);

    /// <summary>
    /// The video element cannot play the recording: on to the camera's next one.
    /// </summary>
    /// <returns>The command that lets go of the recording.</returns>
    public PlayAllCommand Failed()
    {
        Fail();
        return new PlayAllCommand(PlayAllCommandKind.Close);
    }

    /// <summary>
    /// Pauses or plays the tile with the others, as the viewer asks.
    /// </summary>
    /// <param name="paused">Whether everything is paused.</param>
    /// <returns>The command for the video element, or <see langword="null"/>.</returns>
    public PlayAllCommand? SetPaused(bool paused)
    {
        isPaused = paused;
        if (State != PlayAllTileState.Playing)
        {
            return null;
        }

        return new PlayAllCommand(paused ? PlayAllCommandKind.Pause : PlayAllCommandKind.Play);
    }

    private void Fail()
    {
        MoveTo(index + 1);
        if (State == PlayAllTileState.Finished)
        {
            State = PlayAllTileState.Failed;
        }

        StatusText = Translations.CouldNotBePlayed;
    }

    private void MoveTo(int next)
    {
        index = next;
        if (Current is { } segment)
        {
            State = PlayAllTileState.Waiting;
            StatusText = string.Format(
                CultureInfo.CurrentCulture,
                Translations.NextRecordingAt1,
                segment.Start.ToLocalTime().ToString("T", CultureInfo.CurrentCulture));
        }
        else
        {
            State = PlayAllTileState.Finished;
            StatusText = Translations.NoMoreRecordings;
        }
    }
}