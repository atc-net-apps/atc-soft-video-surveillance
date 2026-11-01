namespace AtcSoft.Video.Surveillance.CameraWall.Wpf.App.Diagnostics;

/// <summary>
/// Diagnostics for a long test run (<c>Advanced.EnableDiagnostics</c>). At start-up it writes a report
/// of the software, the machine and the settings; then, every minute, a health sample of the process
/// (CPU, memory, handles, GDI and USER objects, threads, the garbage collector), the UI thread's
/// slowest answer, the recording disk and each running recording (file growth, frame rate, the last
/// packet). Samples go to the log and to CSV files for charting, under <c>{log folder}\diagnostics</c>.
/// A recording that stops growing, a camera that sends nothing while it records, and a window that
/// stops answering are logged as warnings when they happen.
/// </summary>
public sealed partial class DiagnosticsService : BackgroundServiceBase<DiagnosticsService>
{
    private static readonly TimeSpan StallAfter = TimeSpan.FromMinutes(3);
    private static readonly TimeSpan NoPacketsAfter = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RecordingWorkerStaleAfter = TimeSpan.FromMinutes(2);

    private readonly DiagnosticsServiceOptions options;
    private readonly IApplicationSettingsService settingsService;
    private readonly ICameraStorageService cameraStorage;
    private readonly IRecordingService recordingService;
    private readonly IRecordingSegmentationService? recordingWorker;
    private readonly RecordingGrowthWatch growthWatch = new(StallAfter);
    private UiResponsivenessWatch? uiWatch;
    private TimeSpan lastCpu;
    private DateTime lastSampleUtc;

    /// <summary>
    /// Initializes a new instance of the <see cref="DiagnosticsService"/> class.
    /// </summary>
    public DiagnosticsService(
        ILogger<DiagnosticsService> logger,
        DiagnosticsServiceOptions options,
        IApplicationSettingsService settingsService,
        ICameraStorageService cameraStorage,
        IRecordingService recordingService,
        IRecordingSegmentationService? recordingWorker = null)
        : base(logger, Park(options))
    {
        this.options = options;
        this.settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        this.cameraStorage = cameraStorage ?? throw new ArgumentNullException(nameof(cameraStorage));
        this.recordingService = recordingService ?? throw new ArgumentNullException(nameof(recordingService));
        this.recordingWorker = recordingWorker;
    }

    private string Folder => Path.Combine(options.LogPath, "diagnostics");

    /// <inheritdoc />
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        if (options.Enabled)
        {
            Directory.CreateDirectory(Folder);
            var reportPath = Path.Combine(Folder, string.Create(CultureInfo.InvariantCulture, $"system-report-{DateTime.Now:yyyyMMdd-HHmmss}.txt"));
            LogDiagnosticsStarted(reportPath, options.RepeatIntervalSeconds, Folder);

            // The report asks WMI and powercfg, which take a moment: off the start-up path.
            _ = Task.Run(() => WriteSystemReport(reportPath), CancellationToken.None);

            if (System.Windows.Application.Current?.Dispatcher is { } dispatcher)
            {
                uiWatch = new UiResponsivenessWatch(
                    dispatcher,
                    TimeSpan.FromSeconds(2),
                    delay => LogUiFrozen(delay.TotalSeconds),
                    delay => LogUiStillFrozen(delay.TotalSeconds));
                uiWatch.Start();
            }

            using var process = Process.GetCurrentProcess();
            lastCpu = process.TotalProcessorTime;
            lastSampleUtc = DateTime.UtcNow;
        }

        return base.StartAsync(cancellationToken);
    }

    /// <inheritdoc />
    public override Task StopAsync(CancellationToken cancellationToken)
    {
        uiWatch?.Dispose();
        uiWatch = null;
        return base.StopAsync(cancellationToken);
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        uiWatch?.Dispose();
        uiWatch = null;
        base.Dispose();
    }

    /// <inheritdoc />
    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A failed sample is logged; it never stops the samples or the app.")]
    public override Task DoWorkAsync(CancellationToken stoppingToken)
    {
        if (!options.Enabled)
        {
            return Task.CompletedTask;
        }

        try
        {
            Sample();
        }
        catch (Exception ex)
        {
            LogSampleFailed(ex);
        }

        return Task.CompletedTask;
    }

    private static DiagnosticsServiceOptions Park(
        DiagnosticsServiceOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!options.Enabled)
        {
            options.RepeatIntervalSeconds = ushort.MaxValue;
        }

        return options;
    }

    // The size the writer has reached: the folder entry is not updated while the file is open.
    private static long LengthOf(string path)
    {
        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return stream.Length;
        }
        catch (IOException)
        {
            return -1;
        }
        catch (UnauthorizedAccessException)
        {
            return -1;
        }
    }

    private static void AppendCsv(
        string path,
        string header,
        IEnumerable<string> lines)
    {
        var isNew = !File.Exists(path);
        using var writer = new StreamWriter(path, append: true);
        if (isNew)
        {
            writer.WriteLine(header);
        }

        foreach (var line in lines)
        {
            writer.WriteLine(line);
        }
    }

    private static string Csv(params object?[] values)
        => string.Join(
            ',',
            values.Select(v => v switch
            {
                null => string.Empty,
                string text when text.Contains(',', StringComparison.Ordinal) || text.Contains('"', StringComparison.Ordinal)
                    => "\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"",
                IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
                _ => v.ToString(),
            }));

    [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A report that cannot be written is logged; it never stops the app.")]
    private void WriteSystemReport(string reportPath)
    {
        try
        {
            var recording = settingsService.Recording;
            var folders = new[] { recording.RecordingPath, settingsService.CameraDisplay.SnapshotPath, options.LogPath }
                .Concat(cameraStorage.GetAllCameras().Select(c => c.Overrides?.Recording.RecordingPath ?? string.Empty));
            var sections = SystemReport.Collect(folders).ToList();
            sections.Add(new SystemReportSection("Cameras", DescribeCameras()));

            var text = SystemReport.Format(sections) + "[Settings]" + Environment.NewLine + DescribeSettings();
            File.WriteAllText(reportPath, text);
            LogSystemReport(text);
        }
        catch (Exception ex)
        {
            LogSystemReportFailed(ex);
        }
    }

    // What each camera is and how it records; never a password.
    private List<KeyValuePair<string, string>> DescribeCameras()
        => cameraStorage.GetAllCameras()
            .Select(camera =>
            {
                var connection = camera.Connection;
                var source = connection.Source == CameraSource.Usb
                    ? $"USB {connection.Usb?.FriendlyName}"
                    : string.Create(CultureInfo.InvariantCulture, $"{connection.Protocol} {connection.IpAddress}:{connection.Port}/{connection.Path}");
                var recording = camera.Overrides?.Recording;
                return new KeyValuePair<string, string>(
                    camera.Display.DisplayName,
                    string.Create(
                        CultureInfo.InvariantCulture,
                        $"{source}, transport {camera.Stream.RtspTransport}, low latency {camera.Stream.UseLowLatencyMode}, rotation {camera.Display.Rotation}, record on connect {recording?.EnableRecordingOnConnect?.ToString() ?? "default"}, on motion {recording?.EnableRecordingOnMotion?.ToString() ?? "default"}"));
            })
            .ToList();

    private string DescribeSettings()
    {
        var json = new JsonSerializerOptions { WriteIndented = true };
        var text = new System.Text.StringBuilder();
        foreach (var (name, section) in new (string, object)[]
                 {
                     ("General", settingsService.General),
                     ("Connection", settingsService.Connection),
                     ("Performance", settingsService.Performance),
                     ("Recording", settingsService.Recording),
                     ("MotionDetection", settingsService.MotionDetection),
                     ("Advanced", settingsService.Advanced),
                 })
        {
            text.Append("  ").Append(name).Append(": ").AppendLine(JsonSerializer.Serialize(section, section.GetType(), json));
        }

        return text.ToString();
    }

    // The worker that segments, guards the disk and starts recordings again passes every 30 s; one
    // that has not passed for two minutes is stuck, and recordings are no longer looked after.
    private void CheckRecordingWorker()
    {
        if (recordingWorker?.LastPassUtc is { } lastPass &&
            DateTime.UtcNow - lastPass > RecordingWorkerStaleAfter)
        {
            LogRecordingWorkerStale((DateTime.UtcNow - lastPass).TotalMinutes);
        }
    }

    private void Sample()
    {
        CheckRecordingWorker();
        var now = DateTimeOffset.Now;
        using var process = Process.GetCurrentProcess();
        var cpuTime = process.TotalProcessorTime;
        var wall = DateTime.UtcNow - lastSampleUtc;
        var cpu = wall > TimeSpan.Zero
            ? (cpuTime - lastCpu).TotalMilliseconds / (wall.TotalMilliseconds * Environment.ProcessorCount) * 100
            : 0;
        lastCpu = cpuTime;
        lastSampleUtc = DateTime.UtcNow;

        var (gdi, user) = ProcessMetrics.GuiObjects(process);
        var available = ProcessMetrics.SystemMemory()?.AvailableBytes ?? 0;
        var uiDelay = uiWatch?.TakeMaxDelay() ?? TimeSpan.Zero;
        var diskFree = RecordingDiskFreeGb();
        var sessions = recordingService.GetActiveSessions();

        const long Mb = 1024 * 1024;
        var workingSet = process.WorkingSet64 / Mb;
        var privateBytes = process.PrivateMemorySize64 / Mb;
        var managed = GC.GetTotalMemory(forceFullCollection: false) / Mb;
        var gcPause = (long)GC.GetTotalPauseDuration().TotalMilliseconds;
        var handles = process.HandleCount;
        var threads = process.Threads.Count;

        LogHealth(
            cpu,
            workingSet,
            privateBytes,
            managed,
            GC.CollectionCount(0),
            GC.CollectionCount(1),
            GC.CollectionCount(2),
            gcPause,
            handles,
            gdi,
            user,
            threads,
            (long)(available / Mb),
            (long)uiDelay.TotalMilliseconds,
            sessions.Count,
            diskFree);

        var date = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var time = now.ToString("O", CultureInfo.InvariantCulture);
        AppendCsv(
            Path.Combine(Folder, $"health-{date}.csv"),
            "time,cpuPercent,workingSetMb,privateMb,managedMb,gen0,gen1,gen2,gcPauseMs,handles,gdi,user,threads,availableMemoryMb,slowestUiAnswerMs,recordings,recordingDiskFreeGb",
            [Csv(time, Math.Round(cpu, 1), workingSet, privateBytes, managed, GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2), gcPause, handles, gdi, user, threads, available / Mb, (long)uiDelay.TotalMilliseconds, sessions.Count, diskFree)]);

        SampleRecordings(now, time, date, sessions);
    }

    private void SampleRecordings(
        DateTimeOffset now,
        string time,
        string date,
        IReadOnlyList<AtcSoft.Video.Surveillance.Models.RecordingSession> sessions)
    {
        var growth = growthWatch.Observe(
            now,
            sessions.Select(s => new RecordingFile(s.CameraId, s.CameraName, s.CurrentFilePath, LengthOf(s.CurrentFilePath))));

        var lines = new List<string>();
        foreach (var item in growth)
        {
            var recording = item.Recording;
            var pipeline = recordingService.FindPipeline(recording.CameraId);
            var fps = pipeline?.CurrentFps ?? 0;
            var frames = pipeline?.FramesDecoded ?? 0;
            var packetAge = pipeline is { LastPacketUtc: var last } && last > DateTime.MinValue
                ? (DateTime.UtcNow - last).TotalSeconds
                : -1;
            var sizeMb = recording.Length / (1024d * 1024);

            LogRecordingSample(recording.CameraName, item.State, Path.GetFileName(recording.FilePath), sizeMb, item.GrowthBytes / 1024, fps, frames, packetAge);
            switch (item.State)
            {
                case RecordingGrowthState.Stalled:
                    LogRecordingStalled(recording.CameraName, item.StalledFor.TotalMinutes, recording.FilePath, sizeMb, packetAge);
                    break;
                case RecordingGrowthState.Resumed:
                    LogRecordingResumed(recording.CameraName, recording.FilePath);
                    break;
            }

            if (packetAge > NoPacketsAfter.TotalSeconds)
            {
                LogNoPackets(recording.CameraName, packetAge);
            }

            lines.Add(Csv(time, recording.CameraName, item.State, Path.GetFileName(recording.FilePath), Math.Round(sizeMb, 2), item.GrowthBytes / 1024, Math.Round(fps, 1), frames, Math.Round(packetAge, 1)));
        }

        if (lines.Count > 0)
        {
            AppendCsv(
                Path.Combine(Folder, $"recordings-{date}.csv"),
                "time,camera,state,file,sizeMb,growthKb,fps,framesDecoded,lastPacketSecondsAgo",
                lines);
        }
    }

    private string RecordingDiskFreeGb()
    {
        var path = settingsService.Recording.RecordingPath;
        var volume = string.IsNullOrEmpty(path) ? null : FreeSpaceReader.Default.GetVolume(path);
        var free = volume is null ? null : FreeSpaceReader.Default.GetFreeBytes(volume);
        return free is { } bytes
            ? (bytes / (1024d * 1024 * 1024)).ToString("0.0", CultureInfo.InvariantCulture)
            : string.Empty;
    }
}