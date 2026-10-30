namespace AtcSoft.VideoSurveillance.Services;

/// <summary>
/// Implicit connection-type detection for the server: after a network camera
/// first connects, probe it once (per process) while its type is still
/// <see cref="CameraConnectionType.None"/>, and persist a detected value.
/// Never overwrites a value, including one set while the probe was running.
/// </summary>
public sealed partial class CameraConnectionTypeAutoDetector
{
    private readonly ICameraConnectionTypeDetector detector;
    private readonly ICameraStorageService storage;
    private readonly ILogger<CameraConnectionTypeAutoDetector> logger;
    private readonly ConcurrentDictionary<Guid, byte> probedCameras = new();

    public CameraConnectionTypeAutoDetector(
        ICameraConnectionTypeDetector detector,
        ICameraStorageService storage,
        ILogger<CameraConnectionTypeAutoDetector>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(detector);
        ArgumentNullException.ThrowIfNull(storage);

        this.detector = detector;
        this.storage = storage;
        this.logger = logger ?? NullLogger<CameraConnectionTypeAutoDetector>.Instance;
    }

    /// <summary>
    /// Starts a background probe for <paramref name="camera"/> when it
    /// qualifies. Returns the probe task (already completed when the camera
    /// was skipped). Callers on hot paths can ignore the task.
    /// </summary>
    public Task OnCameraConnectedAsync(CameraConfiguration camera)
    {
        ArgumentNullException.ThrowIfNull(camera);

        if (camera.Connection.Source != CameraSource.Network ||
            camera.Connection.ConnectionType != CameraConnectionType.None ||
            string.IsNullOrWhiteSpace(camera.Connection.IpAddress) ||
            !probedCameras.TryAdd(camera.Id, 0))
        {
            return Task.CompletedTask;
        }

        var cameraId = camera.Id;
        var ipAddress = camera.Connection.IpAddress;
        var userName = camera.Authentication.UserName;
        var password = camera.Authentication.Password;

        return Task.Run(async () =>
        {
            var detected = await detector
                .DetectAsync(ipAddress, userName, password)
                .ConfigureAwait(false);

            if (detected == CameraConnectionType.None)
            {
                return;
            }

            // Re-read: the camera may have been edited while we probed.
            var current = storage.GetCameraById(cameraId);
            if (current is null)
            {
                return;
            }

            var updated = CameraConnectionTypeHelper.ApplyDetected(current.Connection.ConnectionType, detected, isExplicit: false);
            if (updated == current.Connection.ConnectionType)
            {
                return;
            }

            current.Connection.ConnectionType = updated;
            storage.AddOrUpdateCamera(current);
            LogDetected(current.Display.DisplayName, cameraId, updated);
        });
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Detected connection type {ConnectionType} for camera '{DisplayName}' ({CameraId})")]
    private partial void LogDetected(string displayName, Guid cameraId, CameraConnectionType connectionType);
}