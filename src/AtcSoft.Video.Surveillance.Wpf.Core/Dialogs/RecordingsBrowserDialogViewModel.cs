namespace AtcSoft.Video.Surveillance.Wpf.Core.Dialogs;

/// <summary>
/// ViewModel for the recordings browser dialog.
/// </summary>
[SuppressMessage("", "S2325:Make properties static", Justification = "XAML binding requires instance properties")]
public partial class RecordingsBrowserDialogViewModel : ViewModelDialogBase, IDisposable
{
    private readonly IApplicationSettingsService settingsService;
    private readonly Func<IEnumerable<string?>>? cameraRecordingPaths;
    private readonly Func<string, AtcSoft.Video.Surveillance.Models.CameraConfiguration?>? cameraByFolder;

    private ObservableCollection<string> selectedCameraFilters = [];

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
        Func<string, AtcSoft.Video.Surveillance.Models.CameraConfiguration?>? cameraByFolder = null)
    {
        this.settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        this.cameraRecordingPaths = cameraRecordingPaths;
        this.cameraByFolder = cameraByFolder;
        selectedCameraFilters.CollectionChanged += OnSelectedCameraFiltersChanged;

        // The archive is read in the background: the dialog opens at once, and the list fills in.
        Loading = LoadRecordingsAsync();
    }

    /// <summary>
    /// Gets the newest reading of the list (opening, Refresh, after an analysis). It completes once
    /// the list shows what it read.
    /// </summary>
    public Task Loading { get; private set; }

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
    /// Gets the cameras that have recordings, by name. A new dictionary each time the cameras
    /// change, because atc:MultiSelectComboBox takes its items as one.
    /// </summary>
    public IDictionary<string, string> CameraFilters { get; private set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets the cameras whose recordings are listed; none ticked lists every camera.
    /// </summary>
    public ObservableCollection<string> SelectedCameraFilters
    {
        get => selectedCameraFilters;
        set
        {
            value ??= [];
            if (ReferenceEquals(value, selectedCameraFilters))
            {
                return;
            }

            selectedCameraFilters.CollectionChanged -= OnSelectedCameraFiltersChanged;
            selectedCameraFilters = value;
            selectedCameraFilters.CollectionChanged += OnSelectedCameraFiltersChanged;
            OnPropertyChanged();
            OnFilterChanged();
        }
    }

    /// <summary>
    /// Gets the text the camera filter shows when no camera is ticked.
    /// </summary>
    public static string AllCamerasText => Translations.AllCameras;

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
    /// <remarks>
    /// A list, not a query: the grid's column filters filter the collection view of a list.
    /// </remarks>
    public IReadOnlyList<RecordingEntry> FilteredRecordings
    {
        get
        {
            var result = Recordings.AsEnumerable();

            // Camera filter
            if (SelectedCameraFilters.Count > 0)
            {
                var cameras = new HashSet<string>(SelectedCameraFilters, StringComparer.OrdinalIgnoreCase);
                result = result.Where(r => cameras.Contains(r.CameraName));
            }

            // Day filter
            result = ApplyDayFilter(result);

            // Time filter
            result = ApplyTimeFilter(result);

            return result.ToList();
        }
    }

    /// <summary>
    /// Gets a value indicating whether a recording can be played. One that
    /// is still being recorded cannot: it would show a black picture.
    /// </summary>
    public bool CanPlay => selectedIsFinished;

    /// <summary>
    /// Gets a value indicating whether a recording can be deleted. One that
    /// is still being recorded cannot: the recorder has the file.
    /// </summary>
    public bool CanDelete => selectedIsFinished;

    /// <summary>
    /// Gets a value indicating whether the selected recording can be analysed for its activity
    /// timeline: a finished recording without an index, while no other is being analysed.
    /// </summary>
    public bool CanAnalyse
        => selectedIsFinished && SelectedRecording!.Activity is null && !IsAnalysing;

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
            [.. SelectedCameraFilters],
            SelectedDayFilter,
            SelectedTimeFilter,
            SelectedRecording?.FilePath);

    /// <summary>
    /// Applies filters and a selection from an earlier browser. Whatever no
    /// longer exists is skipped: a camera without recordings is left out (none
    /// left means all cameras), and a deleted recording leaves nothing selected.
    /// While the list is still being read, the state is applied once it is in.
    /// </summary>
    /// <param name="state">The state to apply.</param>
    public void RestoreState(RecordingsBrowserState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (!Loading.IsCompleted)
        {
            pendingState = state;
            return;
        }

        ApplyState(state);
    }

    private void ApplyState(RecordingsBrowserState state)
    {
        SelectedCameraFilters = [.. state.CameraFilters.Where(CameraFilters.ContainsKey)];

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
    /// <remarks>
    /// The folders are read in the background; the list changes on the calling (UI) thread. A look
    /// while the list is being read, or while the last look is still going, is skipped.
    /// </remarks>
    public async Task<bool> UpdateOngoingRecordingsAsync()
    {
        if (updating || !Loading.IsCompleted || stopping.IsCancellationRequested)
        {
            return false;
        }

        updating = true;
        try
        {
            var version = loadVersion;
            var basePath = GetRecordingsBasePath();
            var cameraPaths = GetCameraRecordingPaths();
            List<string> ongoing = [.. Recordings.Where(r => r.IsBeingRecorded).Select(r => r.FilePath)];
            var known = new HashSet<string>(Recordings.Select(r => r.FilePath), StringComparer.OrdinalIgnoreCase);
            var cancellationToken = stopping.Token;

            var (endedFiles, started) = await Task.Run(
                () => (ReadEnded(ongoing, cancellationToken), ReadEntries(basePath, cameraPaths, known, cancellationToken)),
                cancellationToken).ConfigureAwait(true);

            // A new reading replaced the list meanwhile, or the browser closed.
            if (version != loadVersion || stopping.IsCancellationRequested)
            {
                return false;
            }

            return ApplyOngoingChanges(endedFiles, started);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        finally
        {
            updating = false;
        }
    }

    private bool ApplyOngoingChanges(
        IReadOnlyList<(string FilePath, long Length, TimeSpan Duration)> endedFiles,
        IReadOnlyList<RecordingEntry> startedEntries)
    {
        // Ended: the row stays the same row, so the list keeps its place.
        var ended = false;
        foreach (var (filePath, length, duration) in endedFiles)
        {
            var entry = Recordings.FirstOrDefault(r =>
                r.IsBeingRecorded && string.Equals(r.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
            if (entry is null)
            {
                continue;
            }

            entry.MarkFinished(length, duration);
            ended = true;
        }

        // Started: a file the list does not have yet.
        var known = new HashSet<string>(Recordings.Select(r => r.FilePath), StringComparer.OrdinalIgnoreCase);
        var started = startedEntries.Where(e => known.Add(e.FilePath)).ToList();

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
        Loading = LoadRecordingsAsync();
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
        if (SelectedRecording is null || !CanPlay || !IsFinishedNow())
        {
            return;
        }

        PlayRecordingRequested?.Invoke(this, SelectedRecording);
    }

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private void Delete()
    {
        if (SelectedRecording is null || !CanDelete || !IsFinishedNow())
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

    private void OnSelectedCameraFiltersChanged(object? sender, NotifyCollectionChangedEventArgs e)
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
        AtcSoft.Video.Surveillance.Models.CameraConfiguration? camera,
        CancellationToken cancellationToken = default)
    {
        var settings = RecordingAnalysis.SettingsFor(camera, appSettings);
        var rotation = MediaProbe.GetRotation(videoPath);

        var (displayWidth, displayHeight) = MediaProbe.GetDisplaySize(videoPath);
        var (width, height) = MotionAnalysisSize.For(settings.AnalysisWidth, displayWidth, displayHeight);
        using var reader = Atc.Video.Engine.Capture.GreyFrameFileReader.Open(
            videoPath,
            width,
            height,
            (Atc.Video.Engine.VideoRotation)rotation,
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
    public IReadOnlyList<AtcSoft.Video.Surveillance.Models.MotionZone> ZonesFor(
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
        if (SelectedRecording is not { } recording || !CanAnalyse || !IsFinishedNow())
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
            Loading = LoadRecordingsAsync();
            await Loading.ConfigureAwait(true);
            SelectedRecording = Recordings.FirstOrDefault(r => string.Equals(r.FilePath, recording.FilePath, StringComparison.OrdinalIgnoreCase));
            StatusMessage = Translations.RecordingAnalysed;
        }
        catch (OperationCanceledException)
        {
            // The browser was closed: nothing was written.
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or Atc.Video.Engine.FFmpeg.FFmpegException)
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
            durationFill?.Dispose();
            stopping.Dispose();
        }
    }

    // Asks the file again just before a command uses it: a recording may have started meanwhile.
    private bool IsFinishedNow()
    {
        var finished = IsFinished(SelectedRecording);
        if (finished != selectedIsFinished)
        {
            OnSelectedRecordingChanged();
        }

        return finished;
    }

    private void OnSelectedRecordingChanged()
    {
        // The file is asked once here (and again when a command runs), not on every input event:
        // WPF asks the commands whether they can run at each mouse move and key press.
        selectedIsFinished = IsFinished(SelectedRecording);
        OnPropertyChanged(nameof(CanPlay));
        OnPropertyChanged(nameof(CanDelete));
        OnPropertyChanged(nameof(CanAnalyse));
        CommandManager.InvalidateRequerySuggested();
    }

    // Reads the archive in the background (a large one is thousands of files, each with its
    // activity index) and shows it when it is in. A newer reading wins over an older one.
    private async Task LoadRecordingsAsync()
    {
        if (stopping.IsCancellationRequested)
        {
            return;
        }

        var version = ++loadVersion;
        IsLoading = true;
        StatusMessage = Translations.Loading;

        try
        {
            var basePath = GetRecordingsBasePath();
            var cameraPaths = GetCameraRecordingPaths();
            var cancellationToken = stopping.Token;

            var entries = await Task.Run(
                () => ReadEntries(basePath, cameraPaths, skip: null, cancellationToken),
                cancellationToken).ConfigureAwait(true);

            if (version != loadVersion || stopping.IsCancellationRequested)
            {
                return;
            }

            // Sort by date descending (newest first)
            Recordings = new ObservableCollection<RecordingEntry>(
                entries.OrderByDescending(e => e.RecordingTime));

            UpdateCameraFilters();
            UpdateStatusMessage();
            FillDurations(Recordings);

            if (pendingState is { } state)
            {
                pendingState = null;
                ApplyState(state);
            }
        }
        catch (OperationCanceledException)
        {
            // The browser was closed.
        }
        finally
        {
            if (version == loadVersion)
            {
                IsLoading = false;
            }
        }
    }

    // Reads the lengths not known yet off the UI thread, and shows each as it comes. A new load or
    // the dialog closing stops it.
    private void FillDurations(IReadOnlyList<RecordingEntry> entries)
    {
        var pending = entries.Where(e => e.IsDurationPending).ToList();
        if (pending.Count == 0 || stopping.IsCancellationRequested)
        {
            return;
        }

        durationFill?.Cancel();
        durationFill?.Dispose();
        durationFill = CancellationTokenSource.CreateLinkedTokenSource(stopping.Token);
        var cancellationToken = durationFill.Token;
        var dispatcher = Dispatcher.CurrentDispatcher;

        _ = Task.Run(
            () =>
            {
                foreach (var entry in pending)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    var duration = Durations.Get(entry.FilePath);
                    _ = dispatcher.BeginInvoke(() => entry.SetDuration(duration));
                }
            },
            cancellationToken);
    }

    // The entries of every recording in the folders, except those in skip. Runs in the background.
    private static List<RecordingEntry> ReadEntries(
        string basePath,
        IReadOnlyList<string?> cameraPaths,
        HashSet<string>? skip,
        CancellationToken cancellationToken)
    {
        var entries = new List<RecordingEntry>();
        foreach (var root in RecordingRoots.Collect(basePath, cameraPaths).Where(Directory.Exists))
        {
            foreach (var (filePath, cameraName) in EnumerateRecordingFiles(root))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (skip?.Contains(filePath) == true)
                {
                    continue;
                }

                if (CreateEntry(filePath, cameraName, root) is { } entry)
                {
                    entries.Add(entry);
                }
            }
        }

        return entries;
    }

    // The ongoing recordings that have ended, with their length. Runs in the background.
    private static List<(string FilePath, long Length, TimeSpan Duration)> ReadEnded(
        IReadOnlyList<string> ongoing,
        CancellationToken cancellationToken)
    {
        var ended = new List<(string FilePath, long Length, TimeSpan Duration)>();
        foreach (var filePath in ongoing)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var info = new FileInfo(filePath);
                if (!info.Exists || RecordingPathHelper.IsBeingWritten(filePath))
                {
                    continue;
                }

                ended.Add((filePath, info.Length, Durations.Get(filePath)));
            }
            catch (Exception ex) when (IsFileSystemError(ex))
            {
                // Gone or locked: the next look sees it.
            }
        }

        return ended;
    }

    // Each folder below a root is a camera. Its recordings may sit
    // directly in it or in a folder per day, so the whole camera folder is
    // searched. The thumbnails folder holds no recordings. A folder that goes away while it is
    // read (the cleanup deleting a day), or cannot be read, ends its part of the walk.
    private static IEnumerable<(string FilePath, string CameraName)> EnumerateRecordingFiles(
        string basePath)
    {
        foreach (var cameraFolder in WhileReadable(() => Directory.EnumerateDirectories(basePath)))
        {
            var cameraName = Path.GetFileName(cameraFolder);
            if (cameraName.Equals(RecordingPathHelper.ThumbnailsFolderName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var file in WhileReadable(() => Directory.EnumerateFiles(cameraFolder, "*.*", AllFilesBelow))
                .Where(IsVideoFile))
            {
                yield return (file, cameraName);
            }
        }
    }

    private static readonly EnumerationOptions AllFilesBelow = new()
    {
        RecurseSubdirectories = true,
        IgnoreInaccessible = true,
    };

    private static IEnumerable<string> WhileReadable(
        Func<IEnumerable<string>> enumerate)
    {
        IEnumerator<string> items;
        try
        {
            items = enumerate().GetEnumerator();
        }
        catch (Exception ex) when (IsFileSystemError(ex))
        {
            yield break;
        }

        using (items)
        {
            while (true)
            {
                bool more;
                try
                {
                    more = items.MoveNext();
                }
                catch (Exception ex) when (IsFileSystemError(ex))
                {
                    more = false;
                }

                if (!more)
                {
                    yield break;
                }

                yield return items.Current;
            }
        }
    }

    private static bool IsFileSystemError(Exception ex)
        => ex is IOException or UnauthorizedAccessException;

    // A recording that is still being made has no length yet, and probing
    // it only makes FFmpeg report a broken file.
    // The sparklines already read, kept while the app runs: the list is read again on every
    // refresh and every change of a recording.
    private static readonly ActivitySparklineCache Sparklines = new();

    // Lengths of finished recordings, kept between openings of the browser.
    private static readonly RecordingDurationCache Durations = new();

    // Stops an analysis when the browser closes.
    private readonly CancellationTokenSource stopping = new();
    private CancellationTokenSource? durationFill;

    // Which reading of the list is the newest; an older one that ends later is dropped.
    private int loadVersion;

    // A look for ended and started recordings is going on.
    private bool updating;

    // Filters and a selection to apply once the list is in.
    private RecordingsBrowserState? pendingState;

    // Whether the selected recording is finished (can be played, deleted, analysed), as found when
    // it was selected or the list was last brought up to date.
    private bool selectedIsFinished;

    // Null for a file that went away or cannot be read while the list is read.
    private static RecordingEntry? CreateEntry(
        string filePath,
        string cameraName,
        string basePath)
    {
        try
        {
            return CreateEntryOf(new FileInfo(filePath), cameraName, basePath);
        }
        catch (Exception ex) when (IsFileSystemError(ex))
        {
            return null;
        }
    }

    private static RecordingEntry CreateEntryOf(
        FileInfo info,
        string cameraName,
        string basePath)
    {
        var filePath = info.FullName;
        var recordingTime = RecordingPathHelper.TryParseTimestamp(info.Name, out var timestamp)
            ? timestamp
            : info.CreationTime;

        // A length not read before is read in the background (FillDurations): probing every file here
        // held the app up for close to a minute with a large archive.
        var entry = RecordingPathHelper.IsBeingWritten(filePath)
            ? RecordingEntry.Ongoing(filePath, cameraName, basePath, recordingTime, info.Length)
            : Durations.TryGet(filePath, out var duration)
                ? RecordingEntry.Finished(filePath, cameraName, basePath, recordingTime, info.Length, duration)
                : RecordingEntry.Finished(filePath, cameraName, basePath, recordingTime, info.Length, TimeSpan.Zero, durationPending: true);
        return entry.WithActivity(Sparklines.Get(RecordingPathHelper.BuildActivityPath(filePath)));
    }

    // Each camera's own recording folder, read on the UI thread for the background reading.
    private List<string?> GetCameraRecordingPaths()
        => [.. cameraRecordingPaths?.Invoke() ?? []];

    private string GetRecordingsBasePath()
        => !string.IsNullOrEmpty(settingsService.Recording.RecordingPath)
            ? settingsService.Recording.RecordingPath
            : ApplicationPaths.DefaultRecordingsPath;

    private void UpdateCameraFilters()
    {
        var filters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var cameraName in Recordings
                     .Select(r => r.CameraName)
                     .Distinct(StringComparer.OrdinalIgnoreCase)
                     .OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            filters[cameraName] = cameraName;
        }

        CameraFilters = filters;
        OnPropertyChanged(nameof(CameraFilters));

        // A ticked camera that has no recordings any more is unticked; none left means all cameras.
        foreach (var gone in SelectedCameraFilters.Where(c => !filters.ContainsKey(c)).ToList())
        {
            SelectedCameraFilters.Remove(gone);
        }

        OnPropertyChanged(nameof(FilteredRecordings));
    }

    private void UpdateStatusMessage()
    {
        var count = FilteredRecordings.Count;
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