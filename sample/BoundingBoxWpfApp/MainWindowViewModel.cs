namespace BoundingBoxWpfApp;

/// <summary>
/// The sample's window: a run's pictures one at a time, with the boxes the apps would show,
/// the truth, and the score. The clock is deterministic: every picture was analysed by its time
/// before it is shown, so stepping, playing or opening the same file again shows the same boxes.
/// The debug layers show what the detector saw (the foreground, every blob with what became of
/// it, the ignore zones); the tuning panel runs the detector again over the pictures kept, at
/// once, and can save the settings into the storybook. A recording can be reviewed: stretches
/// marked with a verdict are saved beside it as <c>{video}.review.json</c> and score the run by
/// count.
/// </summary>
[SuppressMessage("", "S2325:Make properties static", Justification = "XAML binding requires instance properties")]
public sealed partial class MainWindowViewModel : ViewModelBase
{
#if DEBUG
    // Without the JIT's optimisations the vector steps are many times slower than in the apps.
    private const string BuildNote = "; a Debug build, many times slower than the apps";
#else
    private const string BuildNote = "";
#endif

    private readonly DispatcherTimer playTimer;
    private readonly DispatcherTimer tuneTimer;
    private SampleRun? current;
    private string? openPath;
    private bool openIsStorybook;
    private bool settingRate;
    private bool settingTuning;
    private int runVersion;
    private SampleDetector? fileDetector;
    private RecordingReview? review;
    private double? markFrom;
    private byte[] composed = [];

    [ObservableProperty]
    private string title = "Bounding box sample";

    [ObservableProperty]
    private string status = "Open a storybook or a recording.";

    [ObservableProperty]
    private WriteableBitmap? picture;

    [ObservableProperty]
    private int analysisWidth = 800;

    [ObservableProperty]
    private int analysisHeight = 450;

    [ObservableProperty(AfterChangedCallback = nameof(OnFrameIndexChanged))]
    private int frameIndex;

    [ObservableProperty]
    private int lastFrameIndex;

    [ObservableProperty]
    private string frameInfo = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<BoundingBox>? detectedBoxes;

    [ObservableProperty]
    private IReadOnlyList<BoundingBox>? truthBoxes;

    [ObservableProperty]
    private IReadOnlyList<BoundingBox>? mustNotBoxes;

    [ObservableProperty]
    private IReadOnlyList<CandidateView> candidates = [];

    [ObservableProperty]
    private int videoWidth = 1280;

    [ObservableProperty]
    private int videoHeight = 720;

    [ObservableProperty]
    private string scoreSummary = string.Empty;

    [ObservableProperty]
    private string detectorTime = string.Empty;

    [ObservableProperty]
    private string scoreJson = string.Empty;

    [ObservableProperty(AfterChangedCallback = nameof(OnAnalysesPerSecondChanged))]
    private double analysesPerSecond = StartupOptions.DefaultAnalysesPerSecond;

    [ObservableProperty]
    private bool isPlaying;

    [ObservableProperty(AfterChangedCallback = nameof(OnLayersChanged))]
    private bool showForeground = true;

    [ObservableProperty(AfterChangedCallback = nameof(OnLayersChanged))]
    private bool showCandidates = true;

    [ObservableProperty(AfterChangedCallback = nameof(OnLayersChanged))]
    private bool showIgnored = true;

    [ObservableProperty]
    private string debugInfo = string.Empty;

    [ObservableProperty(AfterChangedCallback = nameof(OnTuningChanged))]
    private int sensitivity = 30;

    [ObservableProperty(AfterChangedCallback = nameof(OnTuningChanged))]
    private double smallestObjectPercent = MotionDetectorOptions.DefaultSmallestObjectPercent;

    [ObservableProperty(AfterChangedCallback = nameof(OnTuningChanged))]
    private int stoppedObjectSeconds = MotionDetectorOptions.DefaultStoppedObjectSeconds;

    [ObservableProperty(AfterChangedCallback = nameof(OnTuningChanged))]
    private bool ignoreLightingChanges = true;

    [ObservableProperty(AfterChangedCallback = nameof(OnTuningChanged))]
    private double confirmSeconds;

    [ObservableProperty(AfterChangedCallback = nameof(OnTuningChanged))]
    private double minTravelPercent;

    [ObservableProperty(AfterChangedCallback = nameof(OnTuningChanged))]
    private double mergeGapPercent;

    [ObservableProperty(AfterChangedCallback = nameof(OnTuningChanged))]
    private double trackLostSeconds;

    [ObservableProperty(AfterChangedCallback = nameof(OnTuningChanged))]
    private double backgroundSeconds;

    [ObservableProperty(AfterChangedCallback = nameof(OnTuningChanged))]
    private double lightingSettleSeconds;

    [ObservableProperty]
    private string tuningInfo = string.Empty;

    [ObservableProperty]
    private int liveRotation;

    [ObservableProperty]
    private bool isReviewing;

    [ObservableProperty]
    private string reviewInfo = string.Empty;

    [ObservableProperty]
    private int selectedSpanIndex = -1;

    [ObservableProperty]
    private string selectedVerdict = ReviewVerdicts.Correct;

    [ObservableProperty]
    private int spanCount = 1;

    [ObservableProperty]
    private string spanLabel = string.Empty;

    public MainWindowViewModel()
    {
        playTimer = new DispatcherTimer(DispatcherPriority.Normal);
        playTimer.Tick += OnPlayTick;

        // A slider dragged or a number typed runs once it rests, not at every step.
        tuneTimer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = TimeSpan.FromMilliseconds(250) };
        tuneTimer.Tick += OnTuneTick;
        SetTuning(SampleTuning.From(new MotionDetectionSettings()));
    }

    /// <summary>
    /// Gets the analysis rates to choose from: the server's and CameraWall's.
    /// </summary>
    public IReadOnlyList<double> Rates { get; } = [4, 15];

    /// <summary>
    /// Gets the turns the live clock can play the picture at, clockwise.
    /// </summary>
    public IReadOnlyList<int> Rotations { get; } = [0, 90, 180, 270];

    /// <summary>
    /// Gets the verdicts a marked stretch can have.
    /// </summary>
    public IReadOnlyList<string> Verdicts { get; } = ReviewVerdicts.All;

    /// <summary>
    /// Gets the review's marked stretches, as shown in the list.
    /// </summary>
    public ObservableCollection<string> ReviewSpans { get; } = [];

    /// <summary>
    /// Opens a storybook and runs it with its own settings.
    /// </summary>
    /// <param name="path">The storybook file.</param>
    /// <param name="rate">How many pictures a second are analysed.</param>
    public Task OpenStorybookAsync(
        string path,
        double rate)
    {
        openPath = path;
        openIsStorybook = true;
        SetRateQuietly(rate);
        return LoadAsync(fromFile: true);
    }

    /// <summary>
    /// Opens a recording and runs it as <i>Analyse recording</i> does.
    /// </summary>
    /// <param name="path">The recording.</param>
    /// <param name="rate">How many pictures a second are analysed.</param>
    public Task OpenRecordingAsync(
        string path,
        double rate)
    {
        openPath = path;
        openIsStorybook = false;
        SetRateQuietly(rate);
        return LoadAsync(fromFile: true);
    }

    [RelayCommand("OpenStorybook", CanExecute = nameof(CanOpen))]
    private async Task OpenStorybookFromDialogAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Storybook (*.storybook.json)|*.storybook.json|All files (*.*)|*.*" };
        if (dialog.ShowDialog() == true)
        {
            await OpenStorybookAsync(dialog.FileName, AnalysesPerSecond).ConfigureAwait(true);
        }
    }

    [RelayCommand("OpenRecording", CanExecute = nameof(CanOpen))]
    private async Task OpenRecordingFromDialogAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Video (*.mp4;*.mkv)|*.mp4;*.mkv|All files (*.*)|*.*" };
        if (dialog.ShowDialog() == true)
        {
            await OpenRecordingAsync(dialog.FileName, AnalysesPerSecond).ConfigureAwait(true);
        }
    }

    private bool CanOpen()
        => !IsBusy;

    /// <summary>
    /// Sweeps the motion settings over the storybooks in a folder (all of them, at 4 and 15 pictures
    /// a second) and the reviewed recordings in a corpus folder, writes the report to
    /// <c>{data}\sweeps</c> and shows it in the score panel.
    /// </summary>
    /// <param name="scenesFolder">The folder of storybooks.</param>
    /// <param name="corpusFolder">The folder of recordings with their reviews, or <see langword="null"/>.</param>
    public async Task SweepAsync(
        string scenesFolder,
        string? corpusFolder)
    {
        Stop();
        runVersion++;
        IsBusy = true;
        CommandManager.InvalidateRequerySuggested();
        Status = "Sweeping…";
        try
        {
            SweepScene[] scenes = [.. Directory.GetFiles(scenesFolder, "*.storybook.json").Order(StringComparer.Ordinal)
                .Select(f => new SweepScene(Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(f)), StorybookReader.ReadFile(f)))];
            var corpus = CorpusIn(corpusFolder);
            if (corpus.Count > 0 && !EnsureEngine())
            {
                Status = "FFmpeg is needed to read the corpus.";
                return;
            }

            var progress = new Progress<double>(share => Status = string.Create(CultureInfo.InvariantCulture, $"Sweeping… {share:0%}"));
            var ranked = await Task.Run(() => StorybookSweep.Run(scenes, corpus, progress: progress)).ConfigureAwait(true);
            var report = StorybookSweep.Report(ranked);
            var folder = Path.Combine(App.DataFolder, "sweeps");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, string.Create(CultureInfo.InvariantCulture, $"sweep-{DateTime.Now:yyyyMMdd-HHmmss}.md"));
            await File.WriteAllTextAsync(path, report).ConfigureAwait(true);

            current = null;
            fileDetector = null;
            IsReviewing = false;
            Title = "Bounding box sample - sweep";
            ScoreSummary = report.Split('\n').Skip(4).FirstOrDefault()?.Trim() ?? string.Empty;
            DetectorTime = string.Empty;
            ScoreJson = report;
            Log.Information("Swept {Combinations} combinations over {Scenes} scenes and {Recordings} reviewed recordings: {Report}", ranked.Count, scenes.Length, corpus.Count, path);
            Status = "Ready";
        }
        catch (Exception ex) when (ex is StorybookException or IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException or AtcSoft.VideoEngine.FFmpeg.FFmpegException)
        {
            Log.Error(ex, "Sweeping {Folder} failed", scenesFolder);
            Status = $"Failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    [RelayCommand("Sweep", CanExecute = nameof(CanOpen))]
    private async Task SweepFromDialogAsync()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Folder of storybooks to sweep" };
        if (dialog.ShowDialog() == true)
        {
            await SweepAsync(dialog.FolderName, Environment.GetEnvironmentVariable(StartupOptions.CorpusVariable)).ConfigureAwait(true);
        }
    }

    // The recordings under a folder with their reviews beside them; one without its recording is left out.
    private static List<SweepRecording> CorpusIn(string? folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
        {
            return [];
        }

        var corpus = new List<SweepRecording>();
        foreach (var reviewPath in Directory.GetFiles(folder, "*" + RecordingPathHelper.ReviewExtension, SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            var reviewed = RecordingReviewFile.Read(reviewPath);
            var video = Path.Combine(Path.GetDirectoryName(reviewPath)!, reviewed.Recording.File);
            if (File.Exists(video))
            {
                corpus.Add(new SweepRecording(video, reviewed));
            }
        }

        return corpus;
    }

    [RelayCommand(CanExecute = nameof(HasRun))]
    private void PlayPause()
    {
        if (IsPlaying)
        {
            Stop();
            return;
        }

        if (FrameIndex >= LastFrameIndex)
        {
            FrameIndex = 0;
        }

        playTimer.Interval = TimeSpan.FromSeconds(1 / AnalysesPerSecond);
        playTimer.Start();
        IsPlaying = true;
    }

    [RelayCommand(CanExecute = nameof(HasRun))]
    private void PreviousFrame()
    {
        Stop();
        FrameIndex = Math.Max(0, FrameIndex - 1);
    }

    [RelayCommand(CanExecute = nameof(HasRun))]
    private void NextFrame()
    {
        Stop();
        FrameIndex = Math.Min(LastFrameIndex, FrameIndex + 1);
    }

    private bool HasRun()
        => current is not null && !IsBusy;

    /// <summary>
    /// Plays what is open live, in a window of its own: the video streamed to the engine's player
    /// as a camera sends it (a storybook encoded first), turned as chosen, with the tuning.
    /// </summary>
    public async Task PlayLiveAsync()
    {
        if (openPath is not { } path || fileDetector is not { } file || current is null)
        {
            return;
        }

        if (!EnsureEngine())
        {
            Status = "FFmpeg is needed for the live clock.";
            return;
        }

        var tool = Path.Combine(VideoEngineBootstrap.FFmpegFolder!, OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");
        if (!File.Exists(tool))
        {
            Status = "The FFmpeg command-line tool is needed for the live clock.";
            return;
        }

        Stop();
        IsBusy = true;
        CommandManager.InvalidateRequerySuggested();
        Status = "Preparing the live stream…";
        try
        {
            var detector = Tuned(file, fromFile: false);
            var rate = AnalysesPerSecond;
            LiveSource source;
            if (openIsStorybook)
            {
                var scene = StorybookReader.ReadFile(path);
                var (video, truth) = await Task.Run(() => SampleRunner.VideoToStream(path, scene, Path.Combine(App.DataFolder, "videos"))).ConfigureAwait(true);
                source = new LiveSource(scene.Name, video, truth, scene.Video.Fps, scene.Video.Width, scene.Video.Height, scene.Video.DurationSeconds, scene.Expect, detector, rate);
            }
            else
            {
                // The stream does not carry a recording's turn, so the player is told it.
                var stored = (VideoRotation)MediaProbe.GetRotation(path);
                var (shownWidth, shownHeight) = MediaProbe.GetDisplaySize(path);
                var turned = stored is VideoRotation.Rotate90 or VideoRotation.Rotate270;
                source = new LiveSource(
                    Path.GetFileName(path),
                    path,
                    Truth: null,
                    Fps: 0,
                    turned ? shownHeight : shownWidth,
                    turned ? shownWidth : shownHeight,
                    MediaProbe.GetDuration(path).TotalSeconds,
                    new StorybookExpect(),
                    detector,
                    rate,
                    stored);
            }

            var window = new LiveWindow(new LiveWindowViewModel(source, (VideoRotation)LiveRotation, tool, Path.Combine(App.DataFolder, "logs"), PlayerFactory.Value))
            {
                Owner = Application.Current.MainWindow,
            };
            window.Show();
            Status = "Ready";
        }
        catch (Exception ex) when (ex is StorybookException or IOException or InvalidOperationException or AtcSoft.VideoEngine.FFmpeg.FFmpegException)
        {
            Log.Error(ex, "Playing {Path} live failed", path);
            Status = $"Failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    [RelayCommand("PlayLive", CanExecute = nameof(CanTune))]
    private Task PlayLiveFromWindowAsync()
        => PlayLiveAsync();

    // One factory for every live window, with the GPU as in the apps.
    private static readonly Lazy<IVideoPlayerFactory> PlayerFactory = new(
        () => new VideoPlayerFactory(new Serilog.Extensions.Logging.SerilogLoggerFactory(Log.Logger), new D3D11AcceleratorFactory()));

    [RelayCommand(CanExecute = nameof(CanTune))]
    private void ResetTuning()
    {
        // Runs again through the change callbacks, as a change made by hand does; nothing runs when nothing changed.
        SetTuningLoudly(SampleTuning.From(fileDetector!.Settings));
        UpdateTuningInfo();
    }

    private bool CanTune()
        => HasRun() && fileDetector is not null;

    [RelayCommand(CanExecute = nameof(CanSaveStorybookSettings))]
    private void SaveStorybookSettings()
    {
        var path = openPath!;
        var tuning = CurrentTuning();
        var settings = tuning.Settings(fileDetector!.Settings);

        // A scene of the storybook suite: its baseline and its encoded video follow the file.
        var inSuite = path.Contains("Storybook.Tests", StringComparison.OrdinalIgnoreCase);
        if (inSuite && !Confirm($"{Path.GetFileName(path)} is a scene of the storybook suite: its baseline scores change on the next test run, and an encoded scene is encoded again. Save the settings into it?"))
        {
            return;
        }

        try
        {
            StorybookWriter.SaveDetector(path, settings);
            fileDetector = fileDetector with { Settings = StorybookReader.ReadFile(path).Detector.Settings() };
            var saved = SampleTuning.From(settings);
            var inner = tuning with
            {
                Sensitivity = saved.Sensitivity,
                SmallestObjectPercent = saved.SmallestObjectPercent,
                StoppedObjectSeconds = saved.StoppedObjectSeconds,
                IgnoreLightingChanges = saved.IgnoreLightingChanges,
            };
            var innerChanged = inner != saved;
            TuningInfo = $"Saved the settings into {Path.GetFileName(path)}." + (innerChanged ? " The inner options are not settings and were not saved." : string.Empty);
            Log.Information("Saved the detector settings {Settings} into {Path}", SweepSettings.From(settings), path);
        }
        catch (Exception ex) when (ex is StorybookException or IOException or UnauthorizedAccessException)
        {
            Log.Error(ex, "Saving the detector settings into {Path} failed", path);
            TuningInfo = $"Not saved: {ex.Message}";
        }
    }

    private bool CanSaveStorybookSettings()
        => CanTune() && openIsStorybook;

    private static bool Confirm(string message)
    {
        var dialog = new Atc.Wpf.Components.Dialogs.QuestionDialogBox(Application.Current.MainWindow!, "Save into the suite?", message)
        {
            Width = 460,
        };
        return dialog.ShowDialog() == true;
    }

    [RelayCommand(CanExecute = nameof(CanReview))]
    private void MarkStart()
    {
        Stop();
        markFrom = CurrentTime();
        ReviewInfo = string.Create(CultureInfo.InvariantCulture, $"Stretch from {markFrom:0.00} s: go to its last picture and add it.");
        CommandManager.InvalidateRequerySuggested();
    }

    [RelayCommand(CanExecute = nameof(CanAddSpan))]
    private void AddSpan()
    {
        Stop();
        var from = markFrom!.Value;

        // To the end of the picture shown, so the last picture marked is in the stretch.
        var to = CurrentTime() + (1 / AnalysesPerSecond);
        if (to <= from)
        {
            ReviewInfo = "The stretch must end after it starts.";
            return;
        }

        review!.Spans.Add(new ReviewSpan
        {
            From = TimeSpan.FromSeconds(from),
            To = TimeSpan.FromSeconds(to),
            Verdict = SelectedVerdict,
            Count = SelectedVerdict == ReviewVerdicts.FalseBox ? null : Math.Max(1, SpanCount),
            Label = string.IsNullOrWhiteSpace(SpanLabel) ? null : SpanLabel.Trim(),
        });
        markFrom = null;
        ReviewInfo = "Stretch added; save the review to keep it.";
        RefreshReview();
    }

    private bool CanAddSpan()
        => CanReview() && markFrom is not null;

    [RelayCommand(CanExecute = nameof(CanRemoveSpan))]
    private void RemoveSpan()
    {
        review!.Spans.RemoveAt(SelectedSpanIndex);
        RefreshReview();
    }

    private bool CanRemoveSpan()
        => CanReview() && SelectedSpanIndex >= 0 && SelectedSpanIndex < review!.Spans.Count;

    [RelayCommand(CanExecute = nameof(CanReview))]
    private void SaveReview()
    {
        var path = RecordingPathHelper.BuildReviewPath(openPath!);
        try
        {
            RecordingReviewFile.Write(path, review!);
            ReviewInfo = $"Saved {Path.GetFileName(path)}.";
            Log.Information("Saved the review {Path} with {Count} stretches", path, review!.Spans.Count);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            Log.Error(ex, "Saving the review {Path} failed", path);
            ReviewInfo = $"Not saved: {ex.Message}";
        }
    }

    private bool CanReview()
        => IsReviewing && review is not null && HasRun();

    private double CurrentTime()
        => current is { } run && run.Run.Frames.Count > 0
            ? run.Run.Frames[Math.Clamp(FrameIndex, 0, run.Run.Frames.Count - 1)].Time
            : 0;

    // Opens what is chosen: with the file's own settings (fromFile), or again with the tuning (a new rate).
    private async Task LoadAsync(bool fromFile)
    {
        if (openPath is not { } path)
        {
            return;
        }

        Stop();
        runVersion++;
        IsBusy = true;
        CommandManager.InvalidateRequerySuggested();
        Status = "Analysing…";
        try
        {
            var rate = AnalysesPerSecond;
            SampleRun run;
            if (openIsStorybook)
            {
                var scene = StorybookReader.ReadFile(path);
                if (SampleRunner.NeedsEngine(scene) && !EnsureEngine())
                {
                    Status = "FFmpeg is needed for a storybook with a bitrate.";
                    return;
                }

                var detector = Tuned(new SampleDetector(scene.Detector.Settings()), fromFile);
                run = await Task.Run(() => SampleRunner.RunStorybook(path, scene, rate, Path.Combine(App.DataFolder, "videos"), detector)).ConfigureAwait(true);
                review = null;
                ReviewInfo = string.Empty;
            }
            else
            {
                if (!EnsureEngine())
                {
                    Status = "FFmpeg is needed to read a recording.";
                    return;
                }

                var (found, note, isSaved) = await Task.Run(() => ReviewOf(path)).ConfigureAwait(true);
                var detector = Tuned(SampleRunner.RecordingDetector(isSaved ? found : null), fromFile);
                run = await Task.Run(() => SampleRunner.RunRecording(path, rate, detector)).ConfigureAwait(true);
                review = found;
                ReviewInfo = note;
            }

            markFrom = null;
            IsReviewing = !openIsStorybook;
            Show(run, keepFrame: false);
            RefreshReview();
            Log.Information("Opened {Name} ({Way}, {Rate}/s): {Score}", run.Name, run.Way, rate, run.Run.Score.Summary);
            Status = "Ready";
        }
        catch (Exception ex) when (ex is StorybookException or IOException or InvalidOperationException or InvalidDataException or AtcSoft.VideoEngine.FFmpeg.FFmpegException)
        {
            Log.Error(ex, "Opening {Path} failed", path);
            Status = $"Failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            CommandManager.InvalidateRequerySuggested();
        }
    }

    // The file's detector, kept to tune from; with the file's settings shown, or with the tuning on top.
    private SampleDetector Tuned(
        SampleDetector file,
        bool fromFile)
    {
        fileDetector = file;
        if (fromFile)
        {
            SetTuning(SampleTuning.From(file.Settings));
        }

        var tuning = CurrentTuning();
        var settings = tuning.Settings(file.Settings);
        return file with { Settings = settings, Options = tuning.Options(settings) };
    }

    // Runs the detector again over the pictures kept, with the tuning; a newer run or file wins.
    private async Task RetuneAsync()
    {
        if (current is not { } run || fileDetector is not { } file || IsBusy)
        {
            return;
        }

        var version = ++runVersion;
        var detector = Tuned(file, fromFile: false);
        Status = "Retuning…";
        var result = await Task.Run(() => SampleRunner.Rerun(run, detector)).ConfigureAwait(true);
        if (version != runVersion)
        {
            return;
        }

        Show(result, keepFrame: true);
        RefreshReview();
        Log.Information("Retuned {Name} with {Settings} and {Options}: {Score}", result.Name, SweepSettings.From(detector.Settings), detector.Options, result.Run.Score.Summary);
        Status = "Ready";
    }

    private SampleTuning CurrentTuning()
        => new(
            Sensitivity,
            SmallestObjectPercent,
            StoppedObjectSeconds,
            IgnoreLightingChanges,
            ConfirmSeconds,
            MinTravelPercent,
            MergeGapPercent,
            TrackLostSeconds,
            BackgroundSeconds,
            LightingSettleSeconds);

    // Shows a tuning without running again.
    private void SetTuning(SampleTuning tuning)
    {
        settingTuning = true;
        try
        {
            SetTuningLoudly(tuning);
        }
        finally
        {
            settingTuning = false;
        }

        UpdateTuningInfo();
    }

    private void SetTuningLoudly(SampleTuning tuning)
    {
        Sensitivity = tuning.Sensitivity;
        SmallestObjectPercent = tuning.SmallestObjectPercent;
        StoppedObjectSeconds = tuning.StoppedObjectSeconds;
        IgnoreLightingChanges = tuning.IgnoreLightingChanges;
        ConfirmSeconds = tuning.ConfirmSeconds;
        MinTravelPercent = tuning.MinTravelPercent;
        MergeGapPercent = tuning.MergeGapPercent;
        TrackLostSeconds = tuning.TrackLostSeconds;
        BackgroundSeconds = tuning.BackgroundSeconds;
        LightingSettleSeconds = tuning.LightingSettleSeconds;
    }

    private void UpdateTuningInfo()
        => TuningInfo = fileDetector is null
            ? string.Empty
            : CurrentTuning() == SampleTuning.From(fileDetector.Settings)
                ? "The file's own settings."
                : "Tuned: not the file's own settings.";

    private void OnTuningChanged()
    {
        if (settingTuning)
        {
            return;
        }

        UpdateTuningInfo();
        tuneTimer.Stop();
        tuneTimer.Start();
    }

    private void OnTuneTick(
        object? sender,
        EventArgs e)
    {
        tuneTimer.Stop();
        _ = RetuneAsync();
    }

    // The recording's review beside it (saved), or a new one (not saved); one of another file is not used.
    private static (RecordingReview Review, string Note, bool IsSaved) ReviewOf(
        string path)
    {
        var sha256 = RecordingReviewFile.Sha256Of(path);
        var fresh = new RecordingReview { Recording = new ReviewRecording { File = Path.GetFileName(path), Sha256 = sha256 } };
        var reviewPath = RecordingPathHelper.BuildReviewPath(path);
        if (!File.Exists(reviewPath))
        {
            return (fresh, "No review yet: mark stretches and save.", false);
        }

        try
        {
            var read = RecordingReviewFile.Read(reviewPath);
            return string.Equals(read.Recording.Sha256, sha256, StringComparison.Ordinal)
                ? (read, $"Review with {read.Spans.Count} stretches.", true)
                : (fresh, "The review beside it is of another file; starting a new one.", false);
        }
        catch (InvalidDataException ex)
        {
            return (fresh, $"The review beside it cannot be read ({ex.Message}); starting a new one.", false);
        }
    }

    // Shows the review's stretches and scores the run by them.
    private void RefreshReview()
    {
        ReviewSpans.Clear();
        foreach (var span in review?.Spans ?? [])
        {
            var count = span.Count is { } c ? $" x{c}" : string.Empty;
            var label = span.Label is { } l ? " " + l : string.Empty;
            ReviewSpans.Add(string.Create(CultureInfo.InvariantCulture, $"{span.From.TotalSeconds:0.00}-{span.To.TotalSeconds:0.00} s  {span.Verdict}{count}{label}"));
        }

        if (current is { HasTruth: false } run)
        {
            var score = review is { Spans.Count: > 0 } reviewed ? ReviewScorer.Score(run.Run.Frames, reviewed) : null;
            ScoreSummary = score is not null
                ? "Review: " + score.Summary
                : $"{run.Run.Frames.Count(f => f.Detected.Count > 0)} of {run.Run.Frames.Count} pictures with boxes (no truth)";
            ScoreJson = SampleRunner.ScoreJson(run, score);
        }

        ShowFrame();
        CommandManager.InvalidateRequerySuggested();
    }

    private static bool EnsureEngine()
    {
        if (VideoEngineBootstrap.IsInitialized)
        {
            return true;
        }

        if (FFmpegSetup.EnsureReady() is not { } folder)
        {
            return false;
        }

        VideoEngineBootstrap.Initialize(new VideoEngineConfig { FFmpegPath = folder });
        return true;
    }

    private void Show(
        SampleRun run,
        bool keepFrame)
    {
        current = run;
        Title = $"Bounding box sample - {run.Name}";
        VideoWidth = run.Run.VideoWidth;
        VideoHeight = run.Run.VideoHeight;
        if (Picture is not { } bitmap || bitmap.PixelWidth != run.Run.AnalysisWidth || bitmap.PixelHeight != run.Run.AnalysisHeight)
        {
            Picture = new WriteableBitmap(run.Run.AnalysisWidth, run.Run.AnalysisHeight, 96, 96, PixelFormats.Bgra32, palette: null);
            composed = new byte[run.Run.AnalysisWidth * run.Run.AnalysisHeight * 4];
        }

        AnalysisWidth = run.Run.AnalysisWidth;
        AnalysisHeight = run.Run.AnalysisHeight;
        ScoreJson = run.ScoreJson;
        ScoreSummary = run.HasTruth ? run.Run.Score.Summary : $"{run.Run.Frames.Count(f => f.Detected.Count > 0)} of {run.Run.Frames.Count} pictures with boxes (no truth)";
        DetectorTime = string.Create(CultureInfo.InvariantCulture, $"Detector: {run.MeanMilliseconds:0.00} ms a picture on average, at most {run.MaxMilliseconds:0.00} ms ({run.Run.AnalysisWidth} × {run.Run.AnalysisHeight}, with the diagnostics{BuildNote})");
        LastFrameIndex = Math.Max(0, run.Run.Frames.Count - 1);

        // Always draw, also when the index does not change.
        var index = keepFrame ? Math.Min(FrameIndex, LastFrameIndex) : 0;
        if (FrameIndex == index)
        {
            ShowFrame();
        }
        else
        {
            FrameIndex = index;
        }

        CommandManager.InvalidateRequerySuggested();
    }

    private void ShowFrame()
    {
        if (current is null || current.Run.Frames.Count == 0 || Picture is not { } bitmap)
        {
            return;
        }

        var index = Math.Clamp(FrameIndex, 0, current.Run.Frames.Count - 1);
        var frame = current.Run.Frames[index];
        var diagnostics = frame.Diagnostics;
        if (frame.Pixels is { } pixels)
        {
            DebugPicture.Compose(pixels, diagnostics, ShowForeground, ShowIgnored, bitmap.PixelWidth, bitmap.PixelHeight, composed);
            bitmap.WritePixels(new Int32Rect(0, 0, bitmap.PixelWidth, bitmap.PixelHeight), composed, bitmap.PixelWidth * 4, 0);
        }

        DetectedBoxes = frame.Detected.Select(d => ToBox(d.Box, d.TrackId)).ToList();
        TruthBoxes = frame.Truth.Where(t => !t.IsDistractor && t.State == TruthState.MustBox).Select(t => ToBox(t.Box, trackId: null)).ToList();
        MustNotBoxes = frame.Truth.Where(t => t.State == TruthState.MustNotBox).Select(t => ToBox(t.Box, trackId: null)).ToList();
        Candidates = ShowCandidates && diagnostics is not null ? [.. diagnostics.Candidates.Select(CandidateView.Of)] : [];
        DebugInfo = diagnostics is null ? string.Empty : DescribeDiagnostics(diagnostics);
        var marked = review?.Spans.Where(s => s.From.TotalSeconds <= frame.Time && frame.Time < s.To.TotalSeconds).Select(s => s.Verdict + (s.Count is { } c ? $" x{c}" : string.Empty)).ToList() ?? [];
        FrameInfo = string.Create(
            CultureInfo.InvariantCulture,
            $"{frame.Time:0.00} s · picture {index + 1} of {current.Run.Frames.Count} · {frame.Detected.Count} boxes · {TruthBoxes.Count} objects to box · {frame.DetectorMilliseconds:0.00} ms{(marked.Count > 0 ? " · marked: " + string.Join(", ", marked) : string.Empty)}");
    }

    // The detector's stage and its blobs by what became of them, e.g. "analysed · 2 shown, 1 too small".
    private static string DescribeDiagnostics(MotionDiagnostics diagnostics)
    {
        var counts = diagnostics.Candidates
            .GroupBy(c => c.State)
            .OrderBy(g => g.Key)
            .Select(g => $"{g.Count()} {Words(g.Key)}")
            .ToList();
        return CandidateView.Describe(diagnostics.Stage) + (counts.Count > 0 ? " · " + string.Join(", ", counts) : string.Empty);

        static string Words(MotionCandidateState state)
            => state switch
            {
                MotionCandidateState.TooSmall => "too small",
                MotionCandidateState.TooNew => "too new",
                MotionCandidateState.NotTravelled => "not travelled",
                _ => "shown",
            };
    }

    private static BoundingBox ToBox(
        PixelBox box,
        int? trackId)
        => new() { X = box.X, Y = box.Y, Width = box.Width, Height = box.Height, TrackId = trackId };

    private void OnFrameIndexChanged()
        => ShowFrame();

    private void OnLayersChanged()
        => ShowFrame();

    private void OnAnalysesPerSecondChanged()
    {
        // A rate chosen in the window runs the file again with the tuning; one set while opening a file does not.
        if (!settingRate)
        {
            _ = LoadAsync(fromFile: false);
        }
    }

    private void SetRateQuietly(double rate)
    {
        settingRate = true;
        try
        {
            AnalysesPerSecond = rate;
        }
        finally
        {
            settingRate = false;
        }
    }

    private void OnPlayTick(
        object? sender,
        EventArgs e)
    {
        if (FrameIndex >= LastFrameIndex)
        {
            Stop();
            return;
        }

        FrameIndex++;
    }

    private void Stop()
    {
        playTimer.Stop();
        IsPlaying = false;
    }
}