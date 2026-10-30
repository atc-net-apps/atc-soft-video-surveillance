namespace AtcSoft.VideoSurveillance.Wpf.ViewModels;

/// <summary>
/// View model for the Timeline view: every camera as a lane over the last 24 hours or a chosen
/// day, from the server's <c>GET /timeline</c>. A click plays the recording that covers that
/// moment from that far in; previous and next event jump across recordings.
/// </summary>
[SuppressMessage("", "S2325:Make properties static", Justification = "XAML binding requires instance properties")]
public sealed partial class TimelineViewModel : ViewModelBase
{
    // About one stretch per two pixels of a wide window.
    private const int BucketCount = 720;

    // An event plays from a second before it, so its start is seen.
    private static readonly TimeSpan EventLeadIn = TimeSpan.FromSeconds(1);

    // The view grows by itself while it shows a span up to now, at most this often.
    private static readonly TimeSpan LiveReloadInterval = TimeSpan.FromSeconds(5);

    private readonly GatewayService gatewayService;
    private readonly Uri apiBaseUri;
    private readonly ClientSession? session;
    private DateTimeOffset from;
    private DateTimeOffset to;
    private DateTimeOffset lastLoad;

    // A span zoomed or moved by the viewer stays as it is; the last 24 hours and a day reset it.
    private bool zoomed;
    private int reloadPending;

    // Where previous and next count from: the last place played, else an end of the span.
    private DateTimeOffset? cursor;

    [ObservableProperty]
    private ObservableCollection<TimelineLaneViewModel> lanes = [];

    [ObservableProperty]
    private IReadOnlyList<TimelineMark> marks = [];

    [ObservableProperty(AfterChangedCallback = nameof(OnSelectedDayChanged))]
    private DateTime? selectedDay;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimelineViewModel"/> class.
    /// </summary>
    /// <param name="gatewayService">The server.</param>
    /// <param name="apiBaseUri">The server's address, for the recordings' files.</param>
    /// <param name="session">The signed-in session, for the media token.</param>
    /// <param name="eventService">The server's events: the view grows by them while it shows now.</param>
    public TimelineViewModel(
        GatewayService gatewayService,
        Uri apiBaseUri,
        ClientSession? session = null,
        SurveillanceEventService? eventService = null)
    {
        ArgumentNullException.ThrowIfNull(gatewayService);
        ArgumentNullException.ThrowIfNull(apiBaseUri);

        this.gatewayService = gatewayService;
        this.apiBaseUri = apiBaseUri;
        this.session = session;
        if (eventService is not null)
        {
            eventService.OnActivityChanged += _ => ReloadLive();
            eventService.OnRecordingStateChanged += _ => ReloadLive();
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the view is on screen; only then does it grow live.
    /// </summary>
    public bool IsShown { get; set; }

    /// <summary>
    /// Gets the span shown: one day of the viewer's calendar, or the last 24 hours up to now.
    /// </summary>
    /// <param name="day">The day, or <see langword="null"/> for the last 24 hours.</param>
    /// <param name="now">Now.</param>
    /// <param name="zone">The viewer's time zone.</param>
    public static (DateTimeOffset From, DateTimeOffset To) SpanOf(
        DateTime? day,
        DateTimeOffset now,
        TimeZoneInfo zone)
        => AtcSoft.VideoSurveillance.Helpers.Activity.TimelineDays.Span(day, now, zone);

    [RelayCommand("Load")]
    private async Task LoadAsync()
    {
        var now = DateTimeOffset.UtcNow;
        lastLoad = now;
        if (!zoomed)
        {
            (from, to) = SpanOf(SelectedDay, now, TimeZoneInfo.Local);
        }

        IsLoading = true;
        StatusMessage = string.Empty;
        try
        {
            var timeline = await gatewayService.GetTimelineAsync(from, to, BucketCount).ConfigureAwait(true);
            Lanes = [.. (timeline?.Lanes ?? []).Select(l => new TimelineLaneViewModel(l, from, to, now, PlayAt))];
            Marks = TimelineMarks.For(from, to, TimeZoneInfo.Local, CultureInfo.CurrentCulture);
        }
        catch (HttpRequestException ex)
        {
            StatusMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ZoomIn()
        => Zoom(zoomIn: true);

    [RelayCommand]
    private void ZoomOut()
        => Zoom(zoomIn: false);

    [RelayCommand]
    private void Earlier()
        => Move(later: false);

    [RelayCommand]
    private void Later()
        => Move(later: true);

    // Around the place last played, or the middle; the span stays put until the next zoom or move.
    private void Zoom(bool zoomIn)
    {
        var now = DateTimeOffset.UtcNow;
        var focus = cursor ?? from + ((to - from) / 2);
        (from, to) = zoomIn ? TimelineZoom.In(from, to, focus, now) : TimelineZoom.Out(from, to, focus, now);
        zoomed = true;
        LoadCommand.Execute(parameter: null);
    }

    private void Move(bool later)
    {
        (from, to) = TimelineZoom.Move(from, to, later, DateTimeOffset.UtcNow);
        zoomed = true;
        LoadCommand.Execute(parameter: null);
    }

    [RelayCommand]
    private void ShowLast24Hours()
    {
        zoomed = false;
        if (SelectedDay is null)
        {
            LoadCommand.Execute(parameter: null);
            return;
        }

        SelectedDay = null;
    }

    /// <summary>
    /// Gets the tiles of *play all from here*: every lane that has a recording ending after the
    /// moment, with those recordings in time order.
    /// </summary>
    /// <param name="lanes">The lanes shown.</param>
    /// <param name="moment">The moment every tile plays from.</param>
    /// <param name="fileUrl">Turns a recording into the address the player opens.</param>
    /// <returns>One tile per lane that has something to play.</returns>
    public static IReadOnlyList<AtcSoft.VideoSurveillance.Wpf.Core.Windows.PlayAllTileViewModel> PlayAllTiles(
        IEnumerable<TimelineLaneViewModel> lanes,
        DateTimeOffset moment,
        Func<string, Uri> fileUrl)
    {
        ArgumentNullException.ThrowIfNull(lanes);
        ArgumentNullException.ThrowIfNull(fileUrl);

        return [.. lanes
            .Select(l => (Lane: l, Segments: l.Coverage
                .Where(c => c.End > moment)
                .OrderBy(c => c.Start)
                .Select(c => new AtcSoft.VideoSurveillance.Wpf.Core.Windows.PlayAllSegment(fileUrl(c.FilePath), c.Start, c.RecordingStart))
                .ToList()))
            .Where(l => l.Segments.Count > 0)
            .Select(l => new AtcSoft.VideoSurveillance.Wpf.Core.Windows.PlayAllTileViewModel(l.Lane.CameraName, l.Segments))];
    }

    // From the place last clicked or played, else from the first recording in the span.
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "Window disposes its view model on close via the Closed event handler.")]
    [RelayCommand]
    private void PlayAll()
    {
        var moment = cursor ?? Lanes
            .SelectMany(l => l.Coverage)
            .Select(c => c.Start < from ? from : c.Start)
            .DefaultIfEmpty(from)
            .Min();
        var tiles = PlayAllTiles(Lanes, moment, path => RecordingEntryViewModel.FileUrl(apiBaseUri, path, session?.MediaToken));
        if (tiles.Count == 0)
        {
            StatusMessage = Translations.NothingRecordedThen;
            return;
        }

        StatusMessage = string.Empty;
        new AtcSoft.VideoSurveillance.Wpf.Core.Windows.PlayAllWindow(
            new AtcSoft.VideoSurveillance.Wpf.Core.Windows.PlayAllWindowViewModel(moment, tiles)).Show();
    }

    [RelayCommand("NextEvent")]
    private Task NextEventAsync()
        => JumpAsync(forward: true);

    [RelayCommand("PreviousEvent")]
    private Task PreviousEventAsync()
        => JumpAsync(forward: false);

    // Activity started or ended, or a recording started or stopped: reload, a few seconds at most
    // after the last load, while the view shows a span up to now.
    private void ReloadLive()
    {
        if (!IsShown || SelectedDay is not null || zoomed || Interlocked.Exchange(ref reloadPending, 1) == 1)
        {
            return;
        }

        var wait = lastLoad + LiveReloadInterval - DateTimeOffset.UtcNow;
        _ = Task.Run(async () =>
        {
            try
            {
                if (wait > TimeSpan.Zero)
                {
                    await Task.Delay(wait).ConfigureAwait(false);
                }

                if (Application.Current?.Dispatcher is { } dispatcher)
                {
                    await dispatcher.InvokeAsync(() => LoadCommand.Execute(parameter: null));
                }
            }
            finally
            {
                Interlocked.Exchange(ref reloadPending, 0);
            }
        });
    }

    private void OnSelectedDayChanged()
    {
        zoomed = false;
        cursor = null;
        LoadCommand.Execute(parameter: null);
    }

    private async Task JumpAsync(bool forward)
    {
        StatusMessage = string.Empty;
        TimelineEvent? found;
        try
        {
            found = await gatewayService
                .FindTimelineEventAsync(cursor ?? (forward ? from : to), forward)
                .ConfigureAwait(true);
        }
        catch (HttpRequestException ex)
        {
            StatusMessage = ex.Message;
            return;
        }

        if (found is null)
        {
            StatusMessage = Translations.NoEventWithinAWeek;
            return;
        }

        cursor = found.Start;
        var lane = Lanes.FirstOrDefault(l => l.CameraId == found.CameraId);
        var offset = TimeSpan.FromSeconds(found.OffsetSeconds) - EventLeadIn;
        Play(lane, lane?.CameraName ?? string.Empty, found.FilePath, offset > TimeSpan.Zero ? offset : TimeSpan.Zero, found.Start);
    }

    private void PlayAt(
        TimelineLaneViewModel lane,
        double fraction)
    {
        var moment = from + ((to - from) * Math.Clamp(fraction, 0, 1));
        cursor = moment;
        if (lane.Coverage.FirstOrDefault(c => c.Start <= moment && moment < c.End) is not { } covering)
        {
            StatusMessage = Translations.NothingRecordedThen;
            return;
        }

        StatusMessage = string.Empty;
        var offset = moment - covering.RecordingStart;
        Play(lane, lane.CameraName, covering.FilePath, offset > TimeSpan.Zero ? offset : TimeSpan.Zero, moment);
    }

    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "Window self-disposes on close via the Closed event handler.")]
    private void Play(
        TimelineLaneViewModel? lane,
        string cameraName,
        string filePath,
        TimeSpan offset,
        DateTimeOffset moment)
    {
        var url = RecordingEntryViewModel.FileUrl(apiBaseUri, filePath, session?.MediaToken);
        var viewModel = new AtcSoft.VideoSurveillance.Wpf.Core.Windows.FullScreenRecordingWindowViewModel(
            url.AbsoluteUri,
            Title(cameraName, filePath, moment),
            recordingStartTime: (moment - offset).LocalDateTime,
            activityLoader: (buckets, durationSeconds, cancellationToken) => LoadActivityAsync(filePath, buckets, durationSeconds, cancellationToken),
            startPosition: offset,
            nextSegment: lane is null ? null : NextSegments(lane, filePath),
            zonesLoader: lane is null ? null : cancellationToken => CameraZones.LoadAsync(gatewayService, lane.CameraId, cancellationToken),
            previewLoader: ServerPreviews.Loader(gatewayService, FileUrl, filePath),
            verdictSaver: gatewayService.VerdictSaver(filePath));

        new AtcSoft.VideoSurveillance.Wpf.Core.Windows.FullScreenRecordingWindow(viewModel).Show();
    }

    private static string Title(
        string cameraName,
        string filePath,
        DateTimeOffset moment)
        => string.IsNullOrEmpty(cameraName)
            ? System.IO.Path.GetFileName(filePath)
            : $"{cameraName} - {moment.LocalDateTime.ToString("g", CultureInfo.CurrentCulture)}";

    /// <summary>
    /// Gets what plays after a recording of a lane: the lane's following recordings in the span, one
    /// after another, gaps skipped.
    /// </summary>
    /// <param name="lane">The camera's lane.</param>
    /// <param name="filePath">The recording that plays first.</param>
    /// <param name="fileUrl">Turns a recording into the address the player opens.</param>
    /// <param name="loadActivity">Loads a recording's activity for the player's timeline.</param>
    /// <param name="loadPreviews">Loads a recording's preview pictures for the player's timeline; none when <see langword="null"/>.</param>
    /// <param name="verdictSaver">Gets the saver of the verdicts given on a recording's timeline; none when <see langword="null"/>.</param>
    /// <returns>A function that gives the next recording each time, then <see langword="null"/>.</returns>
    public static Func<AtcSoft.VideoSurveillance.Wpf.Core.Windows.RecordingSegment?> NextSegments(
        TimelineLaneViewModel lane,
        string filePath,
        Func<string, Uri> fileUrl,
        Func<string, AtcSoft.VideoSurveillance.Wpf.Core.Services.RecordingActivityLoader> loadActivity,
        Func<string, AtcSoft.VideoSurveillance.Wpf.Core.Services.RecordingPreviewLoader>? loadPreviews = null,
        Func<string, AtcSoft.VideoSurveillance.Wpf.Core.Services.RecordingVerdictSaver>? verdictSaver = null)
    {
        ArgumentNullException.ThrowIfNull(lane);
        ArgumentNullException.ThrowIfNull(fileUrl);
        ArgumentNullException.ThrowIfNull(loadActivity);

        var following = lane.Coverage
            .OrderBy(c => c.Start)
            .SkipWhile(c => !string.Equals(c.FilePath, filePath, StringComparison.Ordinal))
            .Skip(1)
            .ToList();
        var next = 0;
        return () =>
        {
            if (next >= following.Count)
            {
                return null;
            }

            var coverage = following[next++];
            return new AtcSoft.VideoSurveillance.Wpf.Core.Windows.RecordingSegment(
                fileUrl(coverage.FilePath).AbsoluteUri,
                Title(lane.CameraName, coverage.FilePath, coverage.RecordingStart),
                coverage.RecordingStart.LocalDateTime,
                loadActivity(coverage.FilePath),
                loadPreviews?.Invoke(coverage.FilePath),
                verdictSaver?.Invoke(coverage.FilePath));
        };
    }

    private Func<AtcSoft.VideoSurveillance.Wpf.Core.Windows.RecordingSegment?> NextSegments(
        TimelineLaneViewModel lane,
        string filePath)
        => NextSegments(
            lane,
            filePath,
            FileUrl,
            path => (buckets, durationSeconds, cancellationToken) => LoadActivityAsync(path, buckets, durationSeconds, cancellationToken),
            path => ServerPreviews.Loader(gatewayService, FileUrl, path),
            gatewayService.VerdictSaver);

    private Uri FileUrl(string path)
        => RecordingEntryViewModel.FileUrl(apiBaseUri, path, session?.MediaToken);

    private async Task<IReadOnlyList<AtcSoft.VideoSurveillance.Models.ActivityBucket>?> LoadActivityAsync(
        string filePath,
        int buckets,
        double durationSeconds,
        CancellationToken cancellationToken)
    {
        var activity = await gatewayService
            .GetRecordingActivityAsync(filePath, buckets, durationSeconds, cancellationToken)
            .ConfigureAwait(false);

        return activity is { HasIndex: true }
            ? [.. activity.Buckets.Select(b => new AtcSoft.VideoSurveillance.Models.ActivityBucket(b.Level, b.Tracks, b.IsAnalysed) { Zones = b.Zones ?? [] })]
            : null;
    }
}