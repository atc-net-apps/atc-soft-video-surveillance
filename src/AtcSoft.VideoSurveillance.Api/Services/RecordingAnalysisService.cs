namespace AtcSoft.VideoSurveillance.Api.Services;

/// <summary>
/// *Analyse recording* on the server: analyses the queued recordings one at a time, each with its
/// camera's own motion settings and zones, reading the pictures straight from the file
/// (<see cref="AtcSoft.VideoEngine.Capture.GreyFrameFileReader"/>) and writing the activity index beside it
/// (<see cref="RecordingAnalysis"/>). The server names its recordings in UTC.
/// </summary>
public sealed partial class RecordingAnalysisService : BackgroundServiceBase<RecordingAnalysisService>, IRecordingAnalyses
{
    private readonly ConcurrentQueue<string> queue = new();
    private readonly ConcurrentDictionary<string, byte> pending = new(StringComparer.OrdinalIgnoreCase);
    private readonly IApplicationSettingsService settingsService;
    private readonly ICameraStorageService cameraStorageService;
    private readonly IRecordingPreviews? previews;

    public RecordingAnalysisService(
        ILogger<RecordingAnalysisService> logger,
        RecordingAnalysisServiceOptions options,
        IApplicationSettingsService settingsService,
        ICameraStorageService cameraStorageService,
        IRecordingPreviews? previews = null)
        : base(logger, options)
    {
        this.settingsService = settingsService;
        this.cameraStorageService = cameraStorageService;
        this.previews = previews;
    }

    /// <inheritdoc />
    public bool TryEnqueue(string videoPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(videoPath);

        if (!pending.TryAdd(videoPath, 0))
        {
            return false;
        }

        queue.Enqueue(videoPath);
        return true;
    }

    /// <inheritdoc />
    public bool Contains(string videoPath)
        => pending.ContainsKey(videoPath);

    /// <inheritdoc />
    public override Task DoWorkAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested && queue.TryDequeue(out var videoPath))
        {
            try
            {
                var camera = CameraFor(videoPath);
                var watch = Stopwatch.StartNew();
                var pictures = Analyse(videoPath, settingsService.MotionDetection, camera, stoppingToken);
                LogAnalysed(videoPath, pictures, watch.Elapsed.TotalSeconds);

                // Its events get preview tiles of their own now.
                previews?.TryEnqueue(videoPath);
            }
            catch (OperationCanceledException)
            {
                // The server is stopping: nothing was written.
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or AtcSoft.VideoEngine.FFmpeg.FFmpegException)
            {
                LogAnalysisFailed(ex, videoPath);
            }
            finally
            {
                pending.TryRemove(videoPath, out _);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Analyses a recording file for its activity index.
    /// </summary>
    /// <param name="videoPath">The recording.</param>
    /// <param name="appSettings">The server's motion settings.</param>
    /// <param name="camera">The camera the recording is of, whose settings and zones are used; <see langword="null"/> when unknown.</param>
    /// <param name="cancellationToken">Stops the analysis; nothing is written then.</param>
    /// <returns>How many pictures were analysed.</returns>
    public static int Analyse(
        string videoPath,
        MotionDetectionSettings appSettings,
        CameraConfiguration? camera,
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

        return RecordingAnalysis.Run(
            reader.ReadFrames(RecordingAnalysis.FramesPerSecond, cancellationToken).Select(f => (f.Position, f.Pixels)),
            width,
            height,
            RecordingAnalysis.DetectorFor(camera, appSettings, rotation),
            videoPath,
            RecordingAnalysis.StartOf(videoPath, TimeZoneInfo.Utc),
            cancellationToken);
    }

    // The camera whose folder the recording is in, or null.
    private CameraConfiguration? CameraFor(string videoPath)
        => RecordingAnalysis.CameraOf(videoPath, settingsService.Recording.RecordingPath, cameraStorageService.GetAllCameras());

    [LoggerMessage(Level = LogLevel.Information, Message = "Analysed {Path}: {Pictures} pictures in {Seconds:F1} s")]
    private partial void LogAnalysed(string path, int pictures, double seconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not analyse {Path}")]
    private partial void LogAnalysisFailed(Exception ex, string path);
}