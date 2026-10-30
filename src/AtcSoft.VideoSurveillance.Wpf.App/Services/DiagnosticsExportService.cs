namespace AtcSoft.VideoSurveillance.Wpf.App.Services;

public sealed partial class DiagnosticsExportService : IDiagnosticsExportService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    private readonly GatewayService gatewayService;
    private readonly SurveillanceEventService eventService;
    private readonly IApplicationSettingsService settingsService;
    private readonly IAutoStartService autoStartService;
    private readonly string apiBaseAddress;
    private readonly ILogger<DiagnosticsExportService> logger;

    public DiagnosticsExportService(
        GatewayService gatewayService,
        SurveillanceEventService eventService,
        IApplicationSettingsService settingsService,
        IAutoStartService autoStartService,
        string apiBaseAddress,
        ILogger<DiagnosticsExportService> logger)
    {
        ArgumentNullException.ThrowIfNull(gatewayService);
        ArgumentNullException.ThrowIfNull(eventService);
        ArgumentNullException.ThrowIfNull(settingsService);
        ArgumentNullException.ThrowIfNull(autoStartService);
        ArgumentNullException.ThrowIfNull(logger);

        this.gatewayService = gatewayService;
        this.eventService = eventService;
        this.settingsService = settingsService;
        this.autoStartService = autoStartService;
        this.apiBaseAddress = apiBaseAddress;
        this.logger = logger;
    }

    public async Task<DiagnosticsReport> BuildReportAsync(
        CancellationToken cancellationToken = default)
    {
        var report = new DiagnosticsReport
        {
            GeneratedAt = DateTimeOffset.Now,
            Client = BuildClientInfo(),
            Server = new DiagnosticsServerInfo
            {
                ApiBaseAddress = apiBaseAddress,
                LiveUpdatesConnected = eventService.IsConnected,
            },
        };

        try
        {
            var cameras = await gatewayService
                .GetCamerasAsync(cancellationToken)
                .ConfigureAwait(false);

            if (cameras is null)
            {
                // The gateway returned a non-success status — surfaced as null
                // by GatewayService rather than thrown. Tell support so they
                // don't read the empty camera list as "no cameras configured".
                report.Server.GatewayErrorMessage = "Gateway returned no result (non-success status).";
            }
            else
            {
                var mapped = new List<DiagnosticsCameraInfo>(cameras.Length);
                var connected = 0;
                var recording = 0;
                foreach (var camera in cameras)
                {
                    mapped.Add(MapCamera(camera));
                    if (camera.ConnectionState == CameraConnectionState.Connected)
                    {
                        connected++;
                    }

                    if (camera.IsRecording)
                    {
                        recording++;
                    }
                }

                report.Cameras = mapped;
                report.Server.ConnectedCameras = connected;
                report.Server.ActiveRecordings = recording;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            report.Server.GatewayErrorMessage = $"{ex.GetType().Name}: {ex.Message}";
            LogGatewayProbeFailed(ex);
        }

        return report;
    }

    public async Task WriteAsync(
        string filePath,
        DiagnosticsReport report,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(report);

        using var stream = File.Create(filePath);
        await JsonSerializer
            .SerializeAsync(stream, report, JsonOptions, cancellationToken)
            .ConfigureAwait(false);

        LogDiagnosticsExported(filePath);
    }

    private DiagnosticsClientInfo BuildClientInfo()
        => new()
        {
            AppVersion = ApplicationHelper.GetVersion().ToString(),
            OsDescription = RuntimeInformation.OSDescription,
            RuntimeVersion = RuntimeInformation.FrameworkDescription,
            LogPath = settingsService.Advanced.LogPath,
            AutoStartEnabled = autoStartService.IsEnabled,
        };

    private static DiagnosticsCameraInfo MapCamera(Camera camera)
        => new()
        {
            Id = camera.Id,
            DisplayName = camera.DisplayName,
            Source = camera.Source.ToString(),
            IpAddress = string.IsNullOrEmpty(camera.IpAddress) ? null : camera.IpAddress,
            UsbDeviceId = string.IsNullOrEmpty(camera.UsbDeviceId) ? null : camera.UsbDeviceId,
            UsbFriendlyName = string.IsNullOrEmpty(camera.UsbFriendlyName) ? null : camera.UsbFriendlyName,
            ConnectionState = camera.ConnectionState?.ToString(),
            IsRecording = camera.IsRecording,
        };
}