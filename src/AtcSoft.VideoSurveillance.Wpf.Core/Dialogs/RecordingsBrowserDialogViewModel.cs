namespace AtcSoft.VideoSurveillance.Wpf.Core.Dialogs;

/// <summary>
/// ViewModel for the recordings browser dialog.
/// </summary>
[SuppressMessage("", "S2325:Make properties static", Justification = "XAML binding requires instance properties")]
public partial class RecordingsBrowserDialogViewModel : ViewModelDialogBase, IDisposable
{
    private const string AllCamerasKey = "_ALL_";

    private readonly IApplicationSettingsService settingsService;
    private readonly Func<IEnumerable<string?>>? cameraRecordingPaths;
    private readonly Func<string, AtcSoft.VideoSurveillance.Models.CameraConfiguration?>? cameraByFolder;

    [ObservableProperty(AfterChangedCallback = nameof(OnSelectedCameraFilterChanged))]
    private string selectedCameraFilter = AllCamerasKey;

    [ObservableProperty(AfterChangedCallback = nameof(OnSelectedDayFilterChanged))]
    private string selectedDayFilter = DropDownItemsFactory.DefaultDayFilter;

    [ObservableProperty(AfterChangedCallback = nameof(OnSelectedTimeFilterChanged))]
    private string selectedTimeFilter = DropDownItemsFactory.DefaultTimeFilter;

    [ObservableProperty(AfterChangedCallback = nameof(OnSelectedRecordingChanged))]
    private RecordingEntry? selectedRecording;

    [ObservableProperty]
    private ObservableCollection<RecordingEntry> recordings = [];

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="RecordingsBrowserDialogViewModel"/> class.
    /// </summary>
    /// <param name="settingsService">The application settings service.</param>
    /// <param name="cameraRecordingPaths">
    /// Gets each camera's own recording folder (<c>Overrides.Recording.RecordingPath</c>), so
    /// their recordings are listed too; <see langword="null"/> reads the global folder only.
    /// </param>
    /// <param name="cameraByFolder">
    /// Gets the camera whose recordings are in a camera folder, so *Analyse recording* uses its own
    /// motion settings and zones; <see langword="null"/> (or no camera) uses the app's settings.
    /// </param>
    public RecordingsBrowserDialogViewModel(
        IApplicationSettingsService settingsService,
        Func<IEnumerable<string?>>? cameraRecordingPaths = null,
        Func<string, AtcSoft.VideoSurveillance.Models.CameraConfiguration?>? cameraByFolder = null)
    {
        this.settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        this.cameraRecordingPaths = cameraRecordingPaths;
        this.cameraByFolder = cameraByFolder;

        LoadRecordings();
    }

    /// <summary>
    /// Gets or sets the app's previews in the making: a recording played without preview pictures
    /// is queued for them, and so is one analysed. <see langword="null"/> where none are made.
    /// </summary>
    public IRecordingPreviews? Previews { get; set; }

    /// <summary>
    /// Occurs when the dialog requests to be closed.
    /// </summary>
    public event EventHandler<DialogClosedEventArgs>? CloseRequested;

    /// <summary>
    /// Occurs when a recording should be played.
    /// </summary>
    public event EventHandler<RecordingEntry>? PlayRecordingRequested;

    /// <summary>
    /// Occurs when a thumbnail preview is requested.
    /// </summary>
    public event EventHandler<RecordingEntry>? ThumbnailPreviewRequested;

    /// <summary>
    /// Gets the dialog title.
    /// </summary>
    public static string DialogTitle => Translations.BrowseRecordings;

    /// <summary>
    /// Gets the available camera filters.
    /// </summary>
    public ObservableCollection<KeyValuePair<string, string>> CameraFilters { get; } = [];

    /// <summary>
    /// Gets the day filter items for the dropdown.
    /// </summary>
    public IDictionary<string, string> DayFilterItems
        => DropDownItemsFactory.DayFilterItems;

    /// <summary>
    /// Gets the time filter items for the dropdown.
    /// </summary>
    public IDictionary<string, string> TimeFilterItems
        => DropDownItemsFactory.TimeFilterItems;

    /// <summary>
    /// Gets the filtered recordings based on the selected filters.
    /// </summary>
    public IEnumerable<RecordingEntry> FilteredRecordings
    {
        get
        {
            var result = Recordings.AsEnumerable();

            // Camera filter
            if (!string.IsNullOrEmpty(SelectedCameraFilter) && SelectedCameraFilter != AllCamerasKey)
            {
                result = result.Where(r =>
                    string.Equals(r.CameraName, SelectedCameraFilter, StringComparison.OrdinalIgnoreCase));
            }

            // Day filter
            result = ApplyDayFilter(result);

            // Time filter
            result = ApplyTimeFilter(result);

            return result;
        }
    }

    /// <summary>
    /// Gets a value indicating whether a recording can be played. One that
    /// is still being recorded cannot: it would show a black picture.
    /// </summary>
    public bool CanPlay => IsFinished(SelectedRecording);

    /// <summary>
    /// Gets a value indicating whether a recording can be deleted. One that
    /// is still being recorded cannot: the recorder has the file.
    /// </summary>
    public bool CanDelete => IsFinished(SelectedRecording);

    /// <summary>
    /// Gets a value indicating whether the selected recording can be analysed for its activity
    /// timeline: a finished recording without an index, while no other is being analysed.
    /// </summary>
    public bool CanAnalyse
        => IsFinished(SelectedRecording) && SelectedRecording!.Activity is null && !IsAnalysing;

    /// <summary>
    /// Gets a value indicating whether a recording is being analysed.
    /// </summary>
    [ObservableProperty(AfterChangedCallback = nameof(OnSelectedRecordingChanged))]
    private bool isAnalysing;

    /// <summary>
    /// Gets the playback overlay settings from the application settings.
    /// </summary>
    public PlaybackOverlaySettings PlaybackOverlaySettings
        => settingsService.Recording.PlaybackOverlay;

    /// <summary>
    /// Gets a value indicating whether the browser closes when a recording is played.
    /// </summary>
    public bool CloseOnPlay
        => settingsService.Recording.CloseRecordingsBrowserOnPlay;

    /// <summary>
    /// Gets the filters and the selection, to open the browser the same way again.
    /// </summary>
    /// <returns>The current state.</returns>
    public RecordingsBrowserState CaptureState()
        => new(
            SelectedCameraFilter,
            SelectedDayFilter,
            SelectedTimeFilter,
            SelectedRecording?.FilePath);

    /// <summary>
    /// Applies filters and a selection from an earlier browser. Whatever no
    /// longer exists is skipped: a camera without recordings falls back to
    /// all cameras, and a deleted recording leaves nothing selected.
    /// </summary>
    /// <param name="state">The state to apply.</param>
    public void RestoreState(RecordingsBrowserState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (CameraFilters.Any(f => f.Key == state.CameraFilter))
        {
            SelectedCameraFilter = state.CameraFilter;
        }

        if (DayFilterItems.ContainsKey(state.DayFilter))
        {
            SelectedDayFilter = state.DayFilter;
        }

        if (TimeFilterItems.ContainsKey(state.TimeFilter))
        {
            SelectedTimeFilter = state.TimeFilter;
        }

        SelectedRecording = state.SelectedFilePath is null
            ? null
            : FilteredRecordings.FirstOrDefault(r =>
                string.Equals(r.FilePath, state.SelectedFilePath, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Brings the list up to date with what recorders have done since it
    /// was loaded: a recording that has ended becomes an ordinary
    /// recording, and one that has started appears. The open dialog calls
    /// this every few seconds, so nobody has to press Refresh.
    /// </summary>
    /// <returns><see langword="true"/> when something in the list changed.</returns>
    public bool UpdateOngoingRecordings()
    {
        var roots = GetRecordingRoots();
        if (roots.Count == 0)
        {
            return false;
        }

        // Ended: the row stays the same row, so the list keeps its place.
        var ended = false;
        foreach (var entry in Recordings.Where(r => r.IsBeingRecorded).ToList())
        {
            if (!File.Exists(entry.FilePath) || RecordingPathHelper.IsBeingWritten(entry.FilePath))
            {
                continue;
            }

            entry.MarkFinished(new FileInfo(entry.FilePath).Length, MediaProbe.GetDuration(entry.FilePath));
            ended = true;
        }

        // Started: a file the list does not have yet.
        var known = new HashSet<string>(Recordings.Select(r => r.FilePath), StringComparer.OrdinalIgnoreCase);
        var started = EnumerateRecordingFiles(roots)
            .Where(f => !known.Contains(f.FilePath))
            .Select(f => CreateEntry(f.FilePath, f.CameraName, f.Root))
            .ToList();

        if (started.Count > 0)
        {
            var selected = SelectedRecording;

            foreach (var entry in started)
            {
                var index = 0;
                while (index < Recordings.Count && Recordings[index].RecordingTime >= entry.RecordingTime)
                {
                    index++;
                }

                Recordings.Insert(index, entry);
            }

            // The list is rebuilt for the new rows; the selection goes with it.
            UpdateCameraFilters();
            SelectedRecording = selected;
            UpdateStatusMessage();
        }

        if (!ended && started.Count == 0)
        {
            return false;
        }

        OnSelectedRecordingChanged();
        return true;
    }

    [RelayCommand]
    private void Refresh()
    {
        LoadRecordings();
    }

    [RelayCommand]
    private void PreviewThumbnail(RecordingEntry? recording)
    {
        if (recording is null || !recording.HasThumbnail)
        {
            return;
        }

        ThumbnailPreviewRequested?.Invoke(this, recording);
    }

    [RelayCommand(CanExecute = nameof(CanPlay))]
    private void Play()
    {
        if (SelectedRecording is null || !CanPlay)
        {
            return;
        }

        PlayRecordingRequested?.Invoke(this, SelectedRecording);
    }

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private void Delete()
    {
        if (SelectedRecording is null || !CanDelete)
        {
            return;
        }

        try
        {
            // Delete the video file
            File.Delete(SelectedRecording.FilePath);

            // Also delete the thumbnail, wherever its layout put it, and the activity index
            var basePath = SelectedRecording.RecordingRoot ?? GetRecordingsBasePath();
            foreach (var thumbnailPath in RecordingPathHelper.GetCompanionPaths(SelectedRecording.FilePath, basePath))
            {
                try
                {
                    File.Delete(thumbnailPath);
                }
                catch
                {
                    // Ignore companion deletion errors
                }
            }

            // The last recording of a day leaves an empty day folder behind
            _ = MediaCleanupRunner.RemoveEmptyDirectoriesBelow(basePath, MediaCleanupRunner.EmptyDirectoryGracePeriod);

            Recordings.Remove(SelectedRecording);
            SelectedRecording = null;
            UpdateCameraFilters();
            OnPropertyChanged(nameof(FilteredRecordings));
            UpdateStatusMessage();
        }
        catch (IOException ex)
        {
            StatusMessage = string.Format(CultureInfo.CurrentCulture, Translations.FailedWithStatus1, ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            StatusMessage = string.Format(CultureInfo.CurrentCulture, Translations.FailedWithStatus1, ex.Message);
        }
    }

    [RelayCommand]
    private void OpenFolder()
    {
        var basePath = GetRecordingsBasePath();
        if (Directory.Exists(basePath))
        {
            System.Diagnostics.Process.Start(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"), basePath);
        }
    }

    [RelayCommand]
    private void Close()
    {
        CloseRequested?.Invoke(this, new DialogClosedEventArgs(dialogResult: true));
    }

    private void OnSelectedCameraFilterChanged()
        => OnFilterChanged();

    private void OnSelectedDayFilterChanged()
        => OnFilterChanged();

    private void OnSelectedTimeFilterChanged()
        => OnFilterChanged();

    private void OnFilterChanged()
    {
        OnPropertyChanged(nameof(FilteredRecordings));
        UpdateStatusMessage();
    }

    /// <summary>
    /// Analyses a recording file for its activity index, with its camera's motion settings and
    /// zones (the app's settings when the camera is not known): the pictures are read
    /// straight from the file, four a second, as fast as it decodes.
    /// </summary>
    /// <param name="videoPath">The recording.</param>
    /// <param name="appSettings">The app's motion settings.</param>
    /// <param name="camera">The camera the recording is of, or <see langword="null"/>.</param>
    /// <param name="cancellationToken">Stops the analysis; nothing is written then.</param>
    /// <returns>How many pictures were analysed.</returns>
    internal static int AnalyseFile(
        string videoPath,
        MotionDetectionSettings appSettings,
        AtcSoft.VideoSurveillance.Models.CameraConfiguration? camera,
        CancellationToken cancellationToken = default)
    {
        var settings = RecordingAnalysis.SettingsFor(camera, appSettings);
        var rotation = MediaProbe.GetRotation(videoPath);

        var (displayWidth, displayHeight) = MediaProbe.GetDisplaySize(videoPath);
        var (width, height) = MotionAnalysisSize.For(settings.AnalysisWidth, displayWidth, displayHeight);
        using var reader = AtcSoft.VideoEngine.Capture.GreyFrameFileReader.Open(
            videoPath,
            width,
            height,
            (AtcSoft.VideoEngine.VideoRotation)rotation,
            RecordingAnalysis.DecoderThreads);

        // CameraWall names its recordings in local time.
        return RecordingAnalysis.Run(
            reader.ReadFrames(RecordingAnalysis.FramesPerSecond, cancellationToken).Select(f => (f.Position, f.Pixels)),
            width,
            height,
            RecordingAnalysis.DetectorFor(camera, appSettings, rotation),
            videoPath,
            RecordingAnalysis.StartOf(videoPath, TimeZoneInfo.Local),
            cancellationToken);
    }

    /// <summary>
    /// Gets the motion zones of a recording's camera now, for the colours of its timeline; none
    /// when the camera is not known.
    /// </summary>
    /// <param name="recording">The recording.</param>
    public IReadOnlyList<AtcSoft.VideoSurveillance.Models.MotionZone> ZonesFor(
        RecordingEntry recording)
    {
        ArgumentNullException.ThrowIfNull(recording);

        return cameraByFolder?.Invoke(recording.CameraName)?.MotionZones ?? [];
    }

    /// <summary>
    /// Gets the saver of the verdicts given on a recording's timeline: they are written to the
    /// review beside it, a new one with its camera's settings and zones.
    /// </summary>
    /// <param name="recording">The recording.</param>
    public RecordingVerdictSaver VerdictSaverFor(RecordingEntry recording)
    {
        ArgumentNullException.ThrowIfNull(recording);

        return RecordingVerdictSavers.ToFile(recording.FilePath, cameraByFolder?.Invoke(recording.CameraName), settingsService.MotionDetection);
    }

    [RelayCommand("Analyse", CanExecute = nameof(CanAnalyse))]
    private async Task AnalyseAsync()
    {
        if (SelectedRecording is not { } recording || !CanAnalyse)
        {
            return;
        }

        IsAnalysing = true;
        StatusMessage = string.Format(CultureInfo.CurrentCulture, Translations.AnalysingRecording1, recording.FileName);
        try
        {
            var settings = settingsService.MotionDetection;
            var camera = cameraByFolder?.Invoke(recording.CameraName);
            var stoppingToken = stopping.Token;
            await Task.Run(() => AnalyseFile(recording.FilePath, settings, camera, stoppingToken), stoppingToken).ConfigureAwait(true);

            // Its events get preview tiles of their own now.
            Previews?.TryEnqueue(recording.FilePath);
            LoadRecordings();
            SelectedRecording = Recordings.FirstOrDefault(r => string.Equals(r.FilePath, recording.FilePath, StringComparison.OrdinalIgnoreCase));
            StatusMessage = Translations.RecordingAnalysed;
        }
        catch (OperationCanceledException)
        {
            // The browser was closed: nothing was written.
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or AtcSoft.VideoEngine.FFmpeg.FFmpegException)
        {
            StatusMessage = string.Format(CultureInfo.CurrentCulture, Translations.FailedWithStatus1, ex.Message);
        }
        finally
        {
            IsAnalysing = false;
        }
    }

    /// <summary>
    /// Stops what the browser has running in the background (an analysis), as it closes.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Stops the analysis and lets go of what stops it.
    /// </summary>
    /// <param name="disposing">Whether managed resources are let go.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (disposing && !stopping.IsCancellationRequested)
        {
            stopping.Cancel();
            stopping.Dispose();
        }
    }

    private void OnSelectedRecordingChanged()
    {
        OnPropertyChanged(nameof(CanPlay));
        OnPropertyChanged(nameof(CanDelete));
        OnPropertyChanged(nameof(CanAnalyse));
        CommandManager.InvalidateRequerySuggested();
    }

    private void LoadRecordings()
    {
        IsLoading = true;
        StatusMessage = string.Empty;

        try
        {
            var roots = GetRecordingRoots();

            if (roots.Count == 0)
            {
                Recordings.Clear();
                UpdateCameraFilters();
                StatusMessage = Translations.NoRecordingsFound;
                return;
            }

            var entries = EnumerateRecordingFiles(roots)
                .Select(f => CreateEntry(f.FilePath, f.CameraName, f.Root))
                .ToList();

            // Sort by date descending (newest first)
            Recordings = new ObservableCollection<RecordingEntry>(
                entries.OrderByDescending(e => e.RecordingTime));

            UpdateCameraFilters();
            UpdateStatusMessage();
        }
        finally
        {
            IsLoading = false;
        }
    }

    // Each folder below a root is a camera. Its recordings may sit
    // directly in it or in a folder per day, so the whole camera folder is
    // searched. The thumbnails folder holds no recordings.
    private static IEnumerable<(string FilePath, string CameraName, string Root)> EnumerateRecordingFiles(
        IEnumerable<string> roots)
        => roots.SelectMany(root => EnumerateRecordingFiles(root).Select(f => (f.FilePath, f.CameraName, root)));

    private static IEnumerable<(string FilePath, string CameraName)> EnumerateRecordingFiles(
        string basePath)
    {
        foreach (var cameraFolder in Directory.GetDirectories(basePath))
        {
            var cameraName = Path.GetFileName(cameraFolder);
            if (cameraName.Equals(RecordingPathHelper.ThumbnailsFolderName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var file in Directory.EnumerateFiles(cameraFolder, "*.*", SearchOption.AllDirectories)
                .Where(IsVideoFile))
            {
                yield return (file, cameraName);
            }
        }
    }

    // A recording that is still being made has no length yet, and probing
    // it only makes FFmpeg report a broken file.
    // The sparklines already read, kept while the app runs: the list is read again on every
    // refresh and every change of a recording.
    private static readonly ActivitySparklineCache Sparklines = new();

    // Stops an analysis when the browser closes.
    private readonly CancellationTokenSource stopping = new();

    private static RecordingEntry CreateEntry(
        string filePath,
        string cameraName,
        string basePath)
    {
        var info = new FileInfo(filePath);
        var recordingTime = RecordingPathHelper.TryParseTimestamp(info.Name, out var timestamp)
            ? timestamp
            : info.CreationTime;

        var entry = RecordingPathHelper.IsBeingWritten(filePath)
            ? RecordingEntry.Ongoing(filePath, cameraName, basePath, recordingTime, info.Length)
            : RecordingEntry.Finished(filePath, cameraName, basePath, recordingTime, info.Length, MediaProbe.GetDuration(filePath));
        return entry.WithActivity(Sparklines.Get(RecordingPathHelper.BuildActivityPath(filePath)));
    }

    // The global folder and each camera's own folder, each once, that exist.
    private List<string> GetRecordingRoots()
        => RecordingRoots.Collect(GetRecordingsBasePath(), cameraRecordingPaths?.Invoke() ?? [])
            .Where(Directory.Exists)
            .ToList();

    private string GetRecordingsBasePath()
        => !string.IsNullOrEmpty(settingsService.Recording.RecordingPath)
            ? settingsService.Recording.RecordingPath
            : ApplicationPaths.DefaultRecordingsPath;

    private void UpdateCameraFilters()
    {
        var currentSelection = SelectedCameraFilter;

        CameraFilters.Clear();
        CameraFilters.Add(new KeyValuePair<string, string>(AllCamerasKey, Translations.AllCameras));

        var cameraNames = Recordings
            .Select(r => r.CameraName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase);

        foreach (var cameraName in cameraNames)
        {
            CameraFilters.Add(new KeyValuePair<string, string>(cameraName, cameraName));
        }

        // Restore selection if still valid, otherwise default to All
        if (CameraFilters.Any(f => f.Key == currentSelection))
        {
            SelectedCameraFilter = currentSelection;
        }
        else
        {
            SelectedCameraFilter = AllCamerasKey;
        }

        OnPropertyChanged(nameof(FilteredRecordings));
    }

    private void UpdateStatusMessage()
    {
        var count = FilteredRecordings.Count();
        if (count == 0)
        {
            StatusMessage = Translations.NoRecordingsFound;
        }
        else
        {
            StatusMessage = string.Format(
                CultureInfo.CurrentCulture,
                Translations.RecordingsFound1,
                count);
        }
    }

    private static bool IsVideoFile(string path)
    {
        var ext = Path.GetExtension(path).ToUpperInvariant();
        return ext is ".MP4" or ".MKV" or ".AVI";
    }

    // The list is as old as its last refresh, so the file itself is asked:
    // a recording may have started, or ended, since.
    private static bool IsFinished(RecordingEntry? recording)
        => recording is not null && !RecordingPathHelper.IsBeingWritten(recording.FilePath);

    private IEnumerable<RecordingEntry> ApplyDayFilter(
        IEnumerable<RecordingEntry> recordings)
    {
        var today = DateTime.Today;

        return SelectedDayFilter switch
        {
            "_ALL_" => recordings,
            "TODAY" => recordings.Where(r => r.RecordingTime.Date == today),
            "YESTERDAY" => recordings.Where(r => r.RecordingTime.Date == today.AddDays(-1)),
            "LAST7" => recordings.Where(r => r.RecordingTime.Date >= today.AddDays(-6)),
            "LAST30" => recordings.Where(r => r.RecordingTime.Date >= today.AddDays(-29)),
            "THISWEEK" => recordings.Where(r => IsThisWeek(r.RecordingTime, today)),
            "THISMONTH" => recordings.Where(r =>
                r.RecordingTime.Year == today.Year && r.RecordingTime.Month == today.Month),
            _ => recordings,
        };
    }

    private IEnumerable<RecordingEntry> ApplyTimeFilter(
        IEnumerable<RecordingEntry> recordings)
        => SelectedTimeFilter switch
        {
            "_ALL_" => recordings,
            "0_6" => recordings.Where(r => r.RecordingTime.Hour >= 0 && r.RecordingTime.Hour < 6),
            "6_12" => recordings.Where(r => r.RecordingTime.Hour >= 6 && r.RecordingTime.Hour < 12),
            "12_18" => recordings.Where(r => r.RecordingTime.Hour >= 12 && r.RecordingTime.Hour < 18),
            "18_24" => recordings.Where(r => r.RecordingTime.Hour >= 18 && r.RecordingTime.Hour < 24),
            _ => recordings,
        };

    private static bool IsThisWeek(
        DateTime date,
        DateTime today)
    {
        // Get Monday of the current week (using Monday as start of week)
        var daysSinceMonday = ((int)today.DayOfWeek - 1 + 7) % 7;
        var monday = today.AddDays(-daysSinceMonday);
        var sunday = monday.AddDays(6);

        return date.Date >= monday && date.Date <= sunday;
    }
}