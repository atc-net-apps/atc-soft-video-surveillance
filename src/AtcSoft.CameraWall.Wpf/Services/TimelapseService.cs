namespace AtcSoft.CameraWall.Wpf.Services;

/// <summary>
/// Service for managing timelapse capture sessions.
/// Captures PNG snapshots at configurable intervals.
/// </summary>
[Registration(Lifetime.Singleton)]
public partial class TimelapseService : ITimelapseService, IDisposable
{
    private readonly ILogger<TimelapseService> logger;
    private readonly IApplicationSettingsService settingsService;
    private readonly ConcurrentDictionary<Guid, TimelapseCaptureContext> contexts = new();
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="TimelapseService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="settingsService">The application settings service.</param>
    public TimelapseService(
        ILogger<TimelapseService> logger,
        IApplicationSettingsService settingsService)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
    }

    /// <inheritdoc/>
    public event EventHandler<TimelapseFrameCapturedEventArgs>? FrameCaptured;

    /// <inheritdoc/>
    public void StartCapture(
        CameraConfiguration camera,
        IMediaPipeline pipeline)
    {
        ArgumentNullException.ThrowIfNull(camera);
        ArgumentNullException.ThrowIfNull(pipeline);

        // Check if timelapse is enabled for this camera
        if (!GetEffectiveEnabled(camera))
        {
            LogTimelapseNotEnabled(camera.Display.DisplayName);
            return;
        }

        // Stop existing capture if any
        StopCapture(camera.Id);

        var interval = GetEffectiveInterval(camera);
        var context = new TimelapseCaptureContext(camera, pipeline, interval);

        if (!contexts.TryAdd(camera.Id, context))
        {
            LogFailedToAddTimelapseContext(camera.Display.DisplayName);
            return;
        }

        // Start the timer
        context.Timer.Tick += (_, _) => _ = CaptureFrameAsync(camera.Id);
        context.Timer.Start();

        // Capture first frame immediately
        _ = CaptureFrameAsync(camera.Id);

        LogTimelapseCaptureStarted(camera.Display.DisplayName, interval);
    }

    /// <inheritdoc/>
    public void StopCapture(Guid cameraId)
    {
        if (!contexts.TryRemove(cameraId, out var context))
        {
            return;
        }

        context.Timer.Stop();
        context.Dispose();

        LogTimelapseCaptureStopped(context.Camera.Display.DisplayName);
    }

    /// <inheritdoc/>
    public void StopAllCaptures()
    {
        var cameraIds = contexts.Keys.ToList();

        if (cameraIds.Count > 0)
        {
            LogStoppingAllTimelapseCaptures(cameraIds.Count);
        }

        foreach (var cameraId in cameraIds)
        {
            StopCapture(cameraId);
        }
    }

    /// <inheritdoc/>
    public bool IsCapturing(Guid cameraId)
        => contexts.ContainsKey(cameraId);

    /// <inheritdoc/>
    public TimeSpan GetEffectiveInterval(CameraConfiguration camera)
    {
        ArgumentNullException.ThrowIfNull(camera);

        var overrideInterval = camera.Overrides?.Recording.TimelapseInterval;
        if (!string.IsNullOrEmpty(overrideInterval))
        {
            return DropDownItemsFactory.ParseTimelapseInterval(overrideInterval);
        }

        var appInterval = settingsService.Recording.TimelapseInterval;
        return DropDownItemsFactory.ParseTimelapseInterval(appInterval);
    }

    /// <inheritdoc/>
    public bool GetEffectiveEnabled(CameraConfiguration camera)
    {
        ArgumentNullException.ThrowIfNull(camera);

        var overrideEnabled = camera.Overrides?.Recording.EnableTimelapse;
        if (overrideEnabled.HasValue)
        {
            return overrideEnabled.Value;
        }

        return settingsService.Recording.EnableTimelapse;
    }

    /// <summary>
    /// Disposes of the service resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposed)
        {
            return;
        }

        if (disposing)
        {
            StopAllCaptures();
        }

        disposed = true;
    }

    private async Task CaptureFrameAsync(Guid cameraId)
    {
        if (!contexts.TryGetValue(cameraId, out var context))
        {
            return;
        }

        try
        {
            var filePath = GenerateFilePath(context.Camera);

            // Ensure directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Capture snapshot using IMediaPipeline
            var pngBytes = await context.Pipeline.CaptureFrameAsync().ConfigureAwait(false);
            if (pngBytes is null || pngBytes.Length == 0)
            {
                LogTimelapseSnapshotNoData(context.Camera.Display.DisplayName);
                return;
            }

            await File.WriteAllBytesAsync(filePath, pngBytes).ConfigureAwait(false);

            var capturedAt = DateTime.Now;

            LogTimelapseFrameCaptured(context.Camera.Display.DisplayName, filePath);

            // Raise event
            FrameCaptured?.Invoke(
                this,
                new TimelapseFrameCapturedEventArgs(cameraId, filePath, capturedAt));
        }
        catch (Exception ex)
        {
            LogTimelapseFrameCaptureFailed(ex, context.Camera.Display.DisplayName);
        }
    }

    private string GenerateFilePath(CameraConfiguration camera)
    {
        var basePath = GetSnapshotBasePath();
        var safeDisplayName = RecordingPathHelper.GetCameraFolderName(camera.Display.DisplayName, camera.Id);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);

        // Create timelapse subfolder structure: {SnapshotPath}/timelapse/{CameraName}/
        var timelapseFolder = Path.Combine(basePath, "timelapse", safeDisplayName);

        // Generate filename: {CameraName}_{timestamp}.png
        var filename = $"{safeDisplayName}_{timestamp}.png";

        return UniqueFilename.EnsureUnique(Path.Combine(timelapseFolder, filename));
    }

    private string GetSnapshotBasePath()
    {
        var appPath = settingsService.CameraDisplay.SnapshotPath;
        return !string.IsNullOrEmpty(appPath)
            ? appPath
            : ApplicationPaths.DefaultSnapshotsPath;
    }
}