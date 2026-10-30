// ReSharper disable GCSuppressFinalizeForTypeWithoutDestructor
namespace AtcSoft.VideoSurveillance.Wpf.Core.Windows;

/// <summary>
/// ViewModel for the fullscreen recording playback window.
/// Uses WPF MediaElement for local file playback.
/// </summary>
public sealed partial class FullScreenRecordingWindowViewModel : ViewModelDialogBase, IDisposable
{
    /// <summary>
    /// The fewest stretches the activity timeline loads; a longer recording is loaded in one a
    /// second (up to <see cref="ActivityTimelineZoom.MostBuckets"/>), so a minute zoomed in still has its seconds.
    /// </summary>
    public const int ActivityBucketCount = ActivityTimelineZoom.LeastBuckets;

    // How many times previews being made are asked for again: two minutes.
    private const int PreviewRetries = 24;

    private static readonly double[] SpeedOptions = [1.0, 2.0, 4.0, 8.0, 16.0];

    // A jump to an event lands this much before it, so its start is seen; and "previous" right
    // after a jump goes one further back.
    private static readonly TimeSpan EventLeadIn = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan PreviousEventSlack = TimeSpan.FromSeconds(2);

    // Play events only shows this much after each event.
    private static readonly TimeSpan EventLeadOut = TimeSpan.FromSeconds(2);

    // Previews being made are asked for again this often while the window is open.
    private static readonly TimeSpan PreviewRetryInterval = TimeSpan.FromSeconds(5);

    private readonly PlaybackOverlaySettings overlaySettings;
    private readonly Func<RecordingSegment?>? nextSegment;
    private readonly CancellationTokenSource activityCancellation = new();
    private readonly Func<CancellationToken, Task<IReadOnlyList<MotionZone>>>? zonesLoader;
    private string filePath;
    private DateTime? recordingStartTime;
    private RecordingActivityLoader? activityLoader;
    private RecordingPreviewLoader? previewLoader;
    private RecordingVerdictSaver? verdictSaver;
    private TimeSpan startPosition;

    // Counts the recordings played in this window: an activity that arrives for an earlier one is dropped.
    private int segment;
    private IReadOnlyList<ActivityEvent> activityEvents = [];
    private MediaElement? mediaElement;
    private DispatcherTimer? overlayHideTimer;
    private DispatcherTimer? positionUpdateTimer;
    private bool disposed;
    private bool isSeeking;
    private bool isUpdatingPositionFromPlayer;
    private int currentSpeedIndex;

    [ObservableProperty]
    private string fileName = string.Empty;

    [ObservableProperty]
    private bool isOverlayVisible = true;

    [ObservableProperty]
    private bool isPlaying;

    [ObservableProperty]
    private string positionText = "00:00";

    [ObservableProperty]
    private string durationText = "00:00";

    [ObservableProperty]
    private double seekPosition;

    [ObservableProperty]
    private double seekMaximum = 100;

    [ObservableProperty]
    private bool canSeek;

    [ObservableProperty]
    private double playbackSpeed = 1.0;

    [ObservableProperty]
    private string speedText = "1x";

    [ObservableProperty]
    private string recordingTimeText = string.Empty;

    [ObservableProperty]
    private bool showFilename = true;

    [ObservableProperty]
    private SolidColorBrush filenameColor = Brushes.White;

    [ObservableProperty]
    private bool showTimestamp = true;

    [ObservableProperty]
    private SolidColorBrush timestampColor = Brushes.White;

    /// <summary>
    /// The clockwise angle the picture is turned by, read from the
    /// recording's rotation flag. MediaElement ignores that flag, so the
    /// window applies it.
    /// </summary>
    [ObservableProperty]
    private double videoRotationAngle;

    /// <summary>
    /// The activity of the recording for its timeline, or <see langword="null"/> while there is none.
    /// </summary>
    [ObservableProperty]
    private IReadOnlyList<ActivityBucket>? activityBuckets;

    // The brushes of the camera's watch zones by name, for the events of the timeline.
    [ObservableProperty]
    private IReadOnlyDictionary<string, Brush>? zoneBrushes;

    [ObservableProperty]
    private bool hasActivity;

    /// <summary>
    /// Where the player is, as a fraction of the recording, for the timeline.
    /// </summary>
    [ObservableProperty]
    private double activityPosition;

    /// <summary>
    /// How long the recording is, in seconds, for the time the timeline tells under the mouse.
    /// </summary>
    [ObservableProperty]
    private double activityDurationSeconds;

    /// <summary>
    /// When the recording started, in local time, for the time the timeline tells under the mouse.
    /// </summary>
    [ObservableProperty]
    private DateTime? activityRecordingStart;

    /// <summary>
    /// The recording's preview pictures, shown under the mouse on the timeline; none while null.
    /// </summary>
    [ObservableProperty]
    private RecordingPreviewTiles? activityPreviews;

    /// <summary>
    /// Where the part of the recording the timeline shows starts, as a fraction of the recording.
    /// </summary>
    [ObservableProperty]
    private double activityStart;

    /// <summary>
    /// Where the part of the recording the timeline shows ends, as a fraction of the recording.
    /// </summary>
    [ObservableProperty]
    private double activityEnd = 1;

    /// <summary>
    /// What became of the last verdict given on the timeline: saved, or why not.
    /// </summary>
    [ObservableProperty]
    private string verdictMessage = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="FullScreenRecordingWindowViewModel"/> class.
    /// </summary>
    /// <param name="filePath">The path to the recording file.</param>
    /// <param name="fileName">The display name of the recording file.</param>
    /// <param name="overlaySettings">The playback overlay settings.</param>
    /// <param name="recordingStartTime">
    /// When the recording started, in local time. When <see langword="null"/>
    /// it is read from the file name, which is local time for recordings made
    /// by the standalone app. A caller that knows the real start time (the
    /// server names its files in UTC) should pass it.
    /// </param>
    /// <param name="activityLoader">
    /// Gets the recording's activity for the timeline under the player; without one, or for a
    /// recording without an activity index, there is no timeline.
    /// </param>
    /// <param name="startPosition">Where to start in the recording: a timeline plays from a moment.</param>
    /// <param name="nextSegment">
    /// Gets the recording to move on to when this one ends (a timeline plays a camera's segments one
    /// after another); <see langword="null"/>, or a function that returns it, ends playback.
    /// </param>
    /// <param name="zonesLoader">
    /// Gets the camera's motion zones now, so the timeline draws an event in its watch zone's
    /// colour; without one, every event is orange.
    /// </param>
    /// <param name="previewLoader">
    /// Gets the recording's preview pictures, shown under the mouse on the timeline; asked again a
    /// while when they are being made. Without one, the timeline shows words only.
    /// </param>
    /// <param name="verdictSaver">
    /// Saves a verdict given on the timeline (a right-click) in the recording's review; without one,
    /// the timeline offers no verdicts.
    /// </param>
    public FullScreenRecordingWindowViewModel(
        string filePath,
        string fileName,
        PlaybackOverlaySettings? overlaySettings = null,
        DateTime? recordingStartTime = null,
        RecordingActivityLoader? activityLoader = null,
        TimeSpan? startPosition = null,
        Func<RecordingSegment?>? nextSegment = null,
        Func<CancellationToken, Task<IReadOnlyList<MotionZone>>>? zonesLoader = null,
        RecordingPreviewLoader? previewLoader = null,
        RecordingVerdictSaver? verdictSaver = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        this.filePath = filePath;
        this.activityLoader = activityLoader;
        this.previewLoader = previewLoader;
        this.verdictSaver = verdictSaver;
        this.zonesLoader = zonesLoader;
        this.startPosition = startPosition ?? TimeSpan.Zero;
        this.nextSegment = nextSegment;
        this.overlaySettings = overlaySettings ?? new PlaybackOverlaySettings();
        FileName = fileName;
        this.recordingStartTime = recordingStartTime ?? ParseRecordingStartTime(fileName);

        // Apply overlay settings
        ShowFilename = this.overlaySettings.ShowFilename;
        FilenameColor = ParseColor(this.overlaySettings.FilenameColor, Brushes.White);
        ShowTimestamp = this.overlaySettings.ShowTimestamp;
        TimestampColor = ParseColor(this.overlaySettings.TimestampColor, new SolidColorBrush(Color.FromRgb(255, 215, 0)));

        UpdateRecordingTimeText(0);

        StartOverlayHideTimer();
        StartPositionUpdateTimer();
    }

    /// <summary>
    /// Occurs when the window requests to be closed.
    /// </summary>
    public event EventHandler<DialogClosedEventArgs>? CloseRequested;

    /// <summary>
    /// Sets the MediaElement for playback control.
    /// Must be called from code-behind after the window is created.
    /// </summary>
    /// <param name="element">The MediaElement control.</param>
    public void SetMediaElement(MediaElement element)
    {
        mediaElement = element;
        mediaElement.MediaOpened += OnMediaOpened;
        mediaElement.MediaEnded += OnMediaEnded;

        LoadVideoRotation();

        // Open and play the recording
        mediaElement.Source = new Uri(filePath);
        mediaElement.Play();
        IsPlaying = true;
    }

    /// <summary>
    /// Called when the mouse moves in the window.
    /// </summary>
    public void OnMouseMoved()
    {
        IsOverlayVisible = true;
        overlayHideTimer?.Stop();
        overlayHideTimer?.Start();
    }

    /// <summary>
    /// Called when the seek slider drag starts.
    /// </summary>
    public void OnSeekStarted()
    {
        isSeeking = true;
    }

    /// <summary>
    /// Called when the seek slider drag ends.
    /// </summary>
    public void OnSeekCompleted()
    {
        if (isSeeking && mediaElement is not null)
        {
            mediaElement.Position = TimeSpan.FromTicks((long)SeekPosition);
            isSeeking = false;
        }
    }

    /// <summary>
    /// Called when the seek slider value changes.
    /// </summary>
    public void OnSeekValueChanged()
    {
        // Only seek if the change came from user interaction, not from the timer
        if (!isUpdatingPositionFromPlayer && mediaElement is not null)
        {
            mediaElement.Position = TimeSpan.FromTicks((long)SeekPosition);
            UpdateRecordingTimeText((long)SeekPosition);
        }
    }

    private void UpdateRecordingTimeText(long positionTicks)
    {
        if (recordingStartTime.HasValue)
        {
            var currentTime = recordingStartTime.Value.AddTicks(positionTicks);
            RecordingTimeText = currentTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }
        else
        {
            // Fallback to playback position if we couldn't parse the start time
            RecordingTimeText = FormatDuration(positionTicks);
        }
    }

    private static DateTime? ParseRecordingStartTime(string fileName)
        => RecordingPathHelper.TryParseTimestamp(fileName, out var startTime)
            ? startTime
            : null;

    private static SolidColorBrush ParseColor(
        string colorName,
        SolidColorBrush defaultBrush)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(colorName);
            return new SolidColorBrush(color);
        }
        catch
        {
            return defaultBrush;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    [RelayCommand]
    private void PlayPause()
    {
        if (mediaElement is null)
        {
            return;
        }

        if (IsPlaying)
        {
            mediaElement.Pause();
            IsPlaying = false;
        }
        else
        {
            mediaElement.Play();
            IsPlaying = true;
        }
    }

    [RelayCommand]
    private void Close()
    {
        CloseRequested?.Invoke(this, new DialogClosedEventArgs(dialogResult: true));
    }

    [RelayCommand]
    private void CycleSpeed()
    {
        currentSpeedIndex = (currentSpeedIndex + 1) % SpeedOptions.Length;
        PlaybackSpeed = SpeedOptions[currentSpeedIndex];
        SpeedText = $"{PlaybackSpeed:G}x";

        if (mediaElement is not null)
        {
            mediaElement.SpeedRatio = PlaybackSpeed;
        }
    }

    private void LoadVideoRotation()
    {
        // Probing opens the file (or URL), so keep it off the UI thread.
        var path = filePath;
        var dispatcher = Dispatcher.CurrentDispatcher;
        _ = Task.Run(Probe, CancellationToken.None);

        void Probe()
        {
            int degrees;
            try
            {
                degrees = MediaProbe.GetRotation(path);
            }
            catch (Exception)
            {
                // No rotation flag is the same as an unreadable one: play upright.
                return;
            }

            if (degrees != 0)
            {
                _ = dispatcher.BeginInvoke(() => VideoRotationAngle = degrees);
            }
        }
    }

    private void OnMediaOpened(
        object? sender,
        RoutedEventArgs e)
    {
        if (mediaElement?.NaturalDuration.HasTimeSpan == true)
        {
            var duration = mediaElement.NaturalDuration.TimeSpan.Ticks;
            DurationText = FormatDuration(duration);
            SeekMaximum = duration > 0 ? duration : 100;
            CanSeek = duration > 0;

            if (startPosition > TimeSpan.Zero && duration > 0)
            {
                SeekTo(startPosition.Ticks / (double)duration);
            }

            if (activityLoader is not null && duration > 0)
            {
                _ = LoadActivityAsync(activityLoader, segment, TimeSpan.FromTicks(duration).TotalSeconds, Dispatcher.CurrentDispatcher);
                if (zonesLoader is not null && ZoneBrushes is null)
                {
                    _ = LoadZonesAsync(zonesLoader, Dispatcher.CurrentDispatcher);
                }

                if (previewLoader is not null)
                {
                    _ = LoadPreviewsAsync(previewLoader, segment, Dispatcher.CurrentDispatcher);
                }
            }
        }
    }

    private async Task LoadActivityAsync(
        RecordingActivityLoader loader,
        int forSegment,
        double durationSeconds,
        Dispatcher dispatcher)
    {
        IReadOnlyList<ActivityBucket>? buckets;
        try
        {
            buckets = await loader(ActivityTimelineZoom.BucketsFor(durationSeconds), durationSeconds, activityCancellation.Token).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // No activity is the same as an unreadable one: the recording plays without a timeline.
            return;
        }

        if (buckets is not { Count: > 0 })
        {
            return;
        }

        _ = dispatcher.BeginInvoke(() =>
        {
            if (disposed || forSegment != segment)
            {
                return;
            }

            activityEvents = ActivityTimelineLayout.FindEvents(buckets, durationSeconds);
            ActivityDurationSeconds = durationSeconds;
            ActivityRecordingStart = recordingStartTime;
            ActivityStart = 0;
            ActivityEnd = 1;
            ActivityBuckets = buckets;
            HasActivity = true;
            CommandManager.InvalidateRequerySuggested();
        });
    }

    // Previews still being made (a recording from before them) are asked for again a while.
    private async Task LoadPreviewsAsync(
        RecordingPreviewLoader loader,
        int forSegment,
        Dispatcher dispatcher)
    {
        try
        {
            for (var attempt = 0; attempt <= PreviewRetries && !disposed && forSegment == segment; attempt++)
            {
                if (attempt > 0)
                {
                    await Task.Delay(PreviewRetryInterval, activityCancellation.Token).ConfigureAwait(false);
                }

                var load = await loader(activityCancellation.Token).ConfigureAwait(false);
                if (load.Sheet is { } sheet)
                {
                    _ = dispatcher.BeginInvoke(() =>
                    {
                        if (!disposed && forSegment == segment)
                        {
                            ActivityPreviews = sheet;
                        }
                    });
                    return;
                }

                if (!load.IsBeingMade)
                {
                    return;
                }
            }
        }
        catch (Exception)
        {
            // No previews are the same as unreadable ones: the timeline tells in words only.
        }
    }

    // The camera's zones are the same for every segment: loaded once.
    private async Task LoadZonesAsync(
        Func<CancellationToken, Task<IReadOnlyList<MotionZone>>> loader,
        Dispatcher dispatcher)
    {
        IReadOnlyList<MotionZone> zones;
        try
        {
            zones = await loader(activityCancellation.Token).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Without the zones, events are orange.
            return;
        }

        var brushes = MotionZoneColors.ByZone(zones);
        _ = dispatcher.BeginInvoke(() =>
        {
            if (!disposed)
            {
                ZoneBrushes = brushes;
            }
        });
    }

    [RelayCommand]
    private void SeekActivity(double fraction)
        => SeekTo(fraction);

    // A verdict given on the timeline, saved in the review of the recording playing now.
    [RelayCommand("GiveVerdict", CanExecute = nameof(CanGiveVerdict))]
    private async Task GiveVerdictAsync(TimelineVerdict? verdict)
    {
        if (verdict is null || verdictSaver is not { } saver)
        {
            return;
        }

        try
        {
            await saver(verdict, activityCancellation.Token).ConfigureAwait(true);
            VerdictMessage = Translations.VerdictSaved;
        }
        catch (OperationCanceledException)
        {
            // The window is closing.
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or HttpRequestException)
        {
            VerdictMessage = string.Format(CultureInfo.CurrentCulture, Translations.VerdictNotSaved1, ex.Message);
        }
    }

    // The timeline offers verdicts only where it has activity.
    private bool CanGiveVerdict(TimelineVerdict? verdict)
        => verdictSaver is not null;

    [RelayCommand]
    private void ZoomActivity(ActivityTimelineZoomRequest? request)
    {
        if (request is null || !HasActivity)
        {
            return;
        }

        if (request.ZoomIn && ActivityTimelineZoom.CanZoomIn(ActivityStart, ActivityEnd, ActivityDurationSeconds))
        {
            ShowActivity(ActivityTimelineZoom.In(ActivityStart, ActivityEnd, request.Focus, ActivityDurationSeconds));
        }
        else if (!request.ZoomIn && ActivityTimelineZoom.CanZoomOut(ActivityStart, ActivityEnd))
        {
            ShowActivity(ActivityTimelineZoom.Out(ActivityStart, ActivityEnd, request.Focus));
        }
    }

    // The buttons zoom around the player.
    [RelayCommand(CanExecute = nameof(CanZoomActivityIn))]
    private void ZoomActivityIn()
        => ZoomActivity(new ActivityTimelineZoomRequest(ActivityPosition, ZoomIn: true));

    private bool CanZoomActivityIn()
        => HasActivity && ActivityTimelineZoom.CanZoomIn(ActivityStart, ActivityEnd, ActivityDurationSeconds);

    [RelayCommand(CanExecute = nameof(CanZoomActivityOut))]
    private void ZoomActivityOut()
        => ZoomActivity(new ActivityTimelineZoomRequest(ActivityPosition, ZoomIn: false));

    private bool CanZoomActivityOut()
        => HasActivity && ActivityTimelineZoom.CanZoomOut(ActivityStart, ActivityEnd);

    private void ShowActivity((double Start, double End) window)
    {
        ActivityStart = window.Start;
        ActivityEnd = window.End;
        CommandManager.InvalidateRequerySuggested();
    }

    // Moves the timeline's player line, and the part shown with it when the player leaves it.
    private void MoveActivityPosition(double position)
    {
        var window = ActivityTimelineZoom.Follow(ActivityStart, ActivityEnd, ActivityPosition, position);
        ActivityPosition = position;
        if (window != (ActivityStart, ActivityEnd))
        {
            ShowActivity(window);
        }
    }

    [RelayCommand(CanExecute = nameof(CanJumpToEvent))]
    private void NextEvent()
    {
        if (ActivityTimelineLayout.NextEventStart(activityEvents, CurrentFraction() + Fraction(EventLeadIn)) is { } start)
        {
            SeekTo(start - Fraction(EventLeadIn));
        }
    }

    [RelayCommand(CanExecute = nameof(CanJumpToEvent))]
    private void PreviousEvent()
    {
        var at = CurrentFraction() + Fraction(EventLeadIn);
        if (ActivityTimelineLayout.PreviousEventStart(activityEvents, at, Fraction(PreviousEventSlack)) is { } start)
        {
            SeekTo(start - Fraction(EventLeadIn));
        }
    }

    private bool CanJumpToEvent()
        => CanSeek && activityEvents.Count > 0;

    /// <summary>
    /// Gets or sets a value indicating whether only the events are played: the quiet stretches
    /// between them are skipped, and after the last one the player moves on.
    /// </summary>
    [ObservableProperty]
    private bool playEventsOnly;

    // After the last event: the next recording, else the player stops where it is.
    private void FinishEventsOnly()
    {
        if (mediaElement is not null && nextSegment?.Invoke() is { } next)
        {
            PlayNext(mediaElement, next);
            return;
        }

        mediaElement?.Pause();
        IsPlaying = false;
    }

    // On every position tick: skip to the next event, or to the end after the last one.
    private void FollowEventsOnly()
    {
        // Without an index (or before it is read) the recording plays as it is.
        if (!PlayEventsOnly || isSeeking || !IsPlaying || mediaElement is null || !HasActivity || SeekMaximum <= 0)
        {
            return;
        }

        // Analysed, and nothing happened: on to the next recording.
        if (activityEvents.Count == 0)
        {
            FinishEventsOnly();
            return;
        }

        if (ActivityTimelineLayout.EventsOnlyTarget(activityEvents, CurrentFraction(), Fraction(EventLeadIn), Fraction(EventLeadOut)) is not { } target)
        {
            return;
        }

        if (target >= 1)
        {
            FinishEventsOnly();
            return;
        }

        SeekTo(target);
    }

    private double CurrentFraction()
        => mediaElement is not null && SeekMaximum > 0
            ? mediaElement.Position.Ticks / SeekMaximum
            : 0;

    private double Fraction(TimeSpan span)
        => SeekMaximum > 0 ? span.Ticks / SeekMaximum : 0;

    private void SeekTo(double fraction)
    {
        if (mediaElement is null || !CanSeek)
        {
            return;
        }

        var ticks = (long)(Math.Clamp(fraction, 0, 1) * SeekMaximum);
        mediaElement.Position = TimeSpan.FromTicks(ticks);

        isUpdatingPositionFromPlayer = true;
        try
        {
            SeekPosition = ticks;
            PositionText = FormatDuration(ticks);
            MoveActivityPosition(ticks / SeekMaximum);
        }
        finally
        {
            isUpdatingPositionFromPlayer = false;
        }

        UpdateRecordingTimeText(ticks);
    }

    private void OnMediaEnded(
        object? sender,
        RoutedEventArgs e)
    {
        if (mediaElement is not null && nextSegment?.Invoke() is { } next)
        {
            PlayNext(mediaElement, next);
            return;
        }

        IsPlaying = false;
    }

    // Moves on to the next recording in the same window, from its start.
    private void PlayNext(
        MediaElement element,
        RecordingSegment next)
    {
        segment++;
        filePath = next.FilePath;
        FileName = next.FileName;
        recordingStartTime = next.RecordingStartTime ?? ParseRecordingStartTime(next.FileName);
        activityLoader = next.ActivityLoader;
        previewLoader = next.PreviewLoader;
        verdictSaver = next.VerdictSaver;
        VerdictMessage = string.Empty;
        startPosition = TimeSpan.Zero;

        activityEvents = [];
        ActivityPreviews = null;
        ActivityBuckets = null;
        HasActivity = false;
        ActivityPosition = 0;
        ActivityStart = 0;
        ActivityEnd = 1;
        CanSeek = false;
        isUpdatingPositionFromPlayer = true;
        try
        {
            SeekPosition = 0;
            PositionText = FormatDuration(0);
        }
        finally
        {
            isUpdatingPositionFromPlayer = false;
        }

        UpdateRecordingTimeText(0);
        VideoRotationAngle = 0;
        LoadVideoRotation();
        CommandManager.InvalidateRequerySuggested();

        element.Source = new Uri(filePath);
        element.Play();
        IsPlaying = true;
    }

    private void StartOverlayHideTimer()
    {
        overlayHideTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3),
        };

        overlayHideTimer.Tick += (_, _) =>
        {
            IsOverlayVisible = false;
            overlayHideTimer.Stop();
        };

        overlayHideTimer.Start();
    }

    private void StartPositionUpdateTimer()
    {
        positionUpdateTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250),
        };

        positionUpdateTimer.Tick += (_, _) =>
        {
            if (mediaElement is not null)
            {
                var position = mediaElement.Position.Ticks;

                // Always update the recording time text
                UpdateRecordingTimeText(position);

                if (!isSeeking)
                {
                    // Set flag to prevent OnSeekValueChanged from triggering a seek
                    isUpdatingPositionFromPlayer = true;
                    try
                    {
                        SeekPosition = position;
                        PositionText = FormatDuration(position);
                        MoveActivityPosition(SeekMaximum > 0 ? position / SeekMaximum : 0);
                    }
                    finally
                    {
                        isUpdatingPositionFromPlayer = false;
                    }

                    FollowEventsOnly();
                }
            }
        };

        positionUpdateTimer.Start();
    }

    private static string FormatDuration(long ticks)
    {
        var timeSpan = TimeSpan.FromTicks(ticks);
        return timeSpan.Hours > 0
            ? $"{timeSpan.Hours:D2}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}"
            : $"{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
    }

    private void Dispose(bool disposing)
    {
        if (disposed)
        {
            return;
        }

        if (disposing)
        {
            activityCancellation.Cancel();
            activityCancellation.Dispose();

            overlayHideTimer?.Stop();
            overlayHideTimer = null;

            positionUpdateTimer?.Stop();
            positionUpdateTimer = null;

            if (mediaElement is not null)
            {
                mediaElement.MediaOpened -= OnMediaOpened;
                mediaElement.MediaEnded -= OnMediaEnded;
                mediaElement.Stop();
                mediaElement = null;
            }
        }

        disposed = true;
    }
}