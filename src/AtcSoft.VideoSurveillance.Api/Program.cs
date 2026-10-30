// Load advanced settings early to configure logging before Host is built
// ReSharper disable SeparateLocalFunctionsWithJumpStatement
// The data folder is read from the environment and the command line here:
// logging is set up before the host, and its configuration, exist.
// --install-ffmpeg is taken out first: the host would read it as a configuration key.
var installFFmpeg = ServerFFmpegStartup.TakeInstallArgument(args, out var hostArgs);
var earlyConfiguration = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .AddCommandLine(hostArgs)
    .Build();
var advancedSettings = LoadAdvancedSettingsForLogging(
    new ServerDataPaths(earlyConfiguration[ServerDataPaths.ConfigurationKey]).SettingsPath);

// Drop framework Debug noise (Kestrel connection lifecycle, request matching,
// static file middleware) but keep AtcSoft.*
// at Debug and keep Microsoft.* Information+ events (request finished,
// hosting lifetime, etc) for ops visibility.
var loggerConfig = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Information)

    // A stream lease or a media token is in the address of these requests.
    .Filter.ByExcluding(MediaRequestLogFilter.IsMediaRequest)
    .WriteTo.Console(formatProvider: System.Globalization.CultureInfo.InvariantCulture);

if (advancedSettings.EnableDebugLogging)
{
    Directory.CreateDirectory(advancedSettings.LogPath);

    var logFile = Path.Combine(advancedSettings.LogPath, "video-surveillance-api-.log");
    loggerConfig
        .MinimumLevel.Debug()
        .WriteTo.File(
            logFile,
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 7,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
            formatProvider: System.Globalization.CultureInfo.InvariantCulture);
}

// Under Aspire the logs (binding errors included) also go to the dashboard; the sink reads
// the OTEL_EXPORTER_OTLP_* variables Aspire sets (endpoint, headers, protocol, service name).
if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")))
{
    loggerConfig.WriteTo.OpenTelemetry();
}

Log.Logger = loggerConfig.CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(hostArgs);

    builder.Host.UseSerilog();

    // Configure OpenAPI document generation
    builder.Services.AddOpenApi();

    // The generated API's services: ProblemDetails for errors raised outside
    // the handlers, and the enums on the wire as the specification writes them.
    builder.Services.AddVideoSurveillanceApi();

    // Register API handlers from the Domain project (source-generated)
    builder.Services.AddApiHandlersFromDomain();

    // Register Core service implementations for the server. Storage:DataPath
    // moves the server's files out of the shared application data folder, so
    // a second server (or a test) can run with data of its own.
    var dataPaths = new ServerDataPaths(builder.Configuration[ServerDataPaths.ConfigurationKey]);
    builder.Services.AddSingleton(dataPaths);

    // The camera passwords and webhook secrets are encrypted in the data
    // folder, with keys kept in it too: the folder moves as a whole, and a
    // copy of cameras.json alone gives no password away.
    builder.Services
        .AddDataProtection()
        .SetApplicationName("AtcSoft.VideoSurveillance")
        .PersistKeysToFileSystem(new DirectoryInfo(dataPaths.KeysPath));
    builder.Services.AddSingleton<SecretProtector>();
    builder.Services.AddSingleton<ICameraStorageService>(sp =>
        new JsonCameraStorageService(
            dataPaths.CameraDataPath,
            sp.GetRequiredService<SecretProtector>(),
            sp.GetRequiredService<ILogger<JsonCameraStorageService>>()));
    builder.Services.AddSingleton<IApplicationSettingsService>(sp =>
    {
        var settingsService = new JsonApplicationSettingsService(
            dataPaths.SettingsPath,
            sp.GetRequiredService<ILogger<JsonApplicationSettingsService>>());
        dataPaths.RelocateDefaults(settingsService);
        return settingsService;
    });
    builder.Services.AddSingleton<RecordingsFileProvider>();
    builder.Services.AddSingleton<IRecordingDurationProvider, CachedRecordingDurationProvider>();

    // Register both the concrete type and the interface to share one
    // instance — the broadcaster subscribes to ServerRecordingService's
    // server-only CameraConnectionStateChanged event (not on IRecordingService
    // since WPF doesn't need it).
    // One disk-space guard for the recording service and the cleanup: they take turns.
    builder.Services.AddSingleton(new DiskSpaceGuard(FreeSpaceReader.Default));
    builder.Services.AddSingleton<ServerRecordingService>();
    builder.Services.AddSingleton<IRecordingService>(sp =>
        sp.GetRequiredService<ServerRecordingService>());
    builder.Services.AddSingleton<IMotionDetectionService, ServerMotionDetectionService>();

    // Initialize VideoEngine (FFmpeg in-process) and register services.
    // DecoderThreads=1: headless server uses single-threaded decoding per camera
    // to avoid thread_count=0 (auto) hanging in avcodec_open2 with certain codecs.
    // Without the FFmpeg the engine needs the server says what is needed and stops, unless
    // it was started with --install-ffmpeg.
    using var ffmpegLog = new Serilog.Extensions.Logging.SerilogLoggerFactory(Log.Logger);
    using var ffmpegHttp = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
    var ffmpegFolder = await ServerFFmpegStartup
        .EnsureReadyAsync(
            FFmpegRuntime.Check(),
            installFFmpeg,
            (progress, cancellationToken) => FFmpegInstaller.InstallAsync(ffmpegHttp, progress, cancellationToken),
            ffmpegLog.CreateLogger("AtcSoft.VideoSurveillance.Api.FFmpeg"),
            CancellationToken.None)
        .ConfigureAwait(false);
    if (ffmpegFolder is null)
    {
        Environment.ExitCode = 1;
        return;
    }

    VideoEngineBootstrap.Initialize(new VideoEngineConfig { DecoderThreads = 1, FFmpegPath = ffmpegFolder });
    builder.Services.AddSingleton<IVideoPlayerFactory>(sp =>
        new VideoPlayerFactory(sp.GetRequiredService<ILoggerFactory>()));
    builder.Services.AddSingleton<IMediaPipelineFactory, VideoEngineMediaPipelineFactory>();

    // The streaming service asks for the host of the USB cameras' pipelines
    // when it first streams one, so the host is not created just for it.
    builder.Services.AddSingleton(sp => new StreamingService(
        sp.GetRequiredService<ICameraStorageService>(),
        sp.GetRequiredService<ILogger<StreamingService>>(),
        sp));
    builder.Services.AddSingleton<ILiveStreamService>(sp => sp.GetRequiredService<StreamingService>());
    builder.Services.AddSingleton<ICameraConnectionStateSink>(sp => sp.GetRequiredService<ServerRecordingService>());

    // USB-camera support — Null fallback first so non-Windows hosts
    // still compose; Windows then replaces the binding via
    // AddWindowsUsbCameraSupport. The /devices/usb endpoint inspects
    // the bound implementation and returns 503 when it's still the
    // Null fallback.
    builder.Services.AddSingleton<IUsbCameraEnumerator>(NullUsbCameraEnumerator.Instance);
    builder.Services.AddSingleton<IUsbCameraWatcher, NullUsbCameraWatcher>();
    if (OperatingSystem.IsWindows())
    {
        AtcSoft.VideoEngine.Windows.DependencyInjection.ServiceCollectionExtensions
            .AddWindowsUsbCameraSupport(builder.Services);

        // Recording thumbnails are drawn with GDI+. On another platform the
        // recording service simply gets no generator and records without them.
        AtcSoft.VideoEngine.Windows.DependencyInjection.ServiceCollectionExtensions
            .AddWindowsRecordingThumbnails(builder.Services);

        // So are the pictures motion detection compares.
        AtcSoft.VideoEngine.Windows.DependencyInjection.ServiceCollectionExtensions
            .AddWindowsMotionFrameReader(builder.Services);
    }

    // Lifecycle coordinator translates raw watcher events into
    // per-camera Unplugged / Replugged transitions for
    // CameraConnectionService. Singleton so the unplugged-set
    // survives across DoWorkAsync ticks.
    builder.Services.AddSingleton<IUsbCameraLifecycleCoordinator, UsbCameraLifecycleCoordinator>();

    // Connection-type detection (ONVIF): explicit via
    // POST /cameras/{id}/detect-connection-type, implicit after a camera's
    // first successful connect (CameraConnectionService).
    builder.Services.AddSingleton<ICameraConnectionTypeDetector>(sp =>
        new OnvifConnectionTypeDetector(sp.GetRequiredService<ILogger<OnvifConnectionTypeDetector>>()));
    builder.Services.AddSingleton<CameraConnectionTypeAutoDetector>();

    // Configure CORS for the Blazor client.
    // Origins are loaded from configuration (Cors:AllowedOrigins). With AllowCredentials,
    // wildcard origins are forbidden — populate the section explicitly when fronting the
    // API from a non-loopback host.
    string[] defaultAllowedOrigins =
    [
        "http://localhost:5000",
        "https://localhost:5001",
        "http://localhost:39576",   // Blazor.App default HTTP
        "https://localhost:39575",  // Blazor.App default HTTPS
    ];
    var allowedOrigins = builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>() ?? defaultAllowedOrigins;

    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(policy =>
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyMethod()
                  .AllowAnyHeader()
                  .AllowCredentials();
        });
    });

    // Shared health tracker for all BackgroundServiceBase-derived workers.
    // Each worker reports its running state around every DoWorkAsync tick and
    // sets its own max-staleness window; the /health endpoint reads this to
    // surface a wedged or stopped background service.
    builder.Services.AddSingleton<ITimeProvider, SystemTimeProvider>();
    builder.Services.AddSingleton<IBackgroundServiceHealthService, BackgroundServiceHealthService>();

    // Event broadcaster: publishes Core events to GET /events. Built on
    // BackgroundServiceBase for consistency with the other workers (it
    // subscribes/unsubscribes in Start/Stop; DoWorkAsync is a no-op and the
    // idle loop parks on a long delay).
    // The camera events of GET /events, which the broadcaster publishes.
    builder.Services.AddSingleton<SurveillanceEventFeed>();
    builder.Services.AddSingleton(new SurveillanceEventBroadcasterOptions());
    builder.Services.AddHostedService<SurveillanceEventBroadcaster>();

    // An activity index beside every recording, for the timelines.
    builder.Services.AddSingleton(new ActivitySummaryCache());
    builder.Services.AddSingleton(new ActivitySparklineCache());
    builder.Services.AddSingleton(new ActivityIndexServiceOptions());
    builder.Services.AddSingleton<ActivityIndexService>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<ActivityIndexService>());

    // When each camera could be recorded and when not: the timeline's gaps say why.
    builder.Services.AddSingleton(new AvailabilityHistoryServiceOptions());
    builder.Services.AddSingleton<AvailabilityHistoryService>();
    builder.Services.AddSingleton<IAvailabilityHistory>(sp => sp.GetRequiredService<AvailabilityHistoryService>());
    builder.Services.AddHostedService(sp => sp.GetRequiredService<AvailabilityHistoryService>());

    // Analyse recording: one recording at a time in the background.
    builder.Services.AddSingleton(new RecordingAnalysisServiceOptions());
    builder.Services.AddSingleton<RecordingAnalysisService>();
    builder.Services.AddSingleton<IRecordingAnalyses>(sp => sp.GetRequiredService<RecordingAnalysisService>());
    builder.Services.AddHostedService(sp => sp.GetRequiredService<RecordingAnalysisService>());

    // The verdicts on recordings that the timelines give, written to the reviews beside them.
    builder.Services.AddSingleton<IRecordingReviewWriter, RecordingReviewWriter>();

    // The preview pictures of each recording that ends, one at a time in the background.
    builder.Services.AddSingleton(new RecordingPreviewServiceOptions());
    builder.Services.AddSingleton<RecordingPreviewService>();
    builder.Services.AddSingleton<IRecordingPreviews>(sp => sp.GetRequiredService<RecordingPreviewService>());
    builder.Services.AddHostedService(sp => sp.GetRequiredService<RecordingPreviewService>());

    // GET /devices/usb is cached a few seconds; a camera plugged in or pulled out drops it.
    builder.Services.AddSingleton(new UsbDeviceCacheEvictorOptions());
    builder.Services.AddHostedService<UsbDeviceCacheEvictor>();

    // Each BackgroundServiceBase-derived worker gets its OWN typed options
    // instance so they can run on independent intervals — a single shared
    // IBackgroundServiceOptions singleton would force them all onto one cadence.

    // Camera connection manager: auto-records cameras on connect and reaps
    // dead sessions every 30s.
    builder.Services.AddSingleton(new CameraConnectionServiceOptions());

    // One instance, as the hosted worker and as the host of the one
    // pipeline a USB camera has for watching and recording alike.
    builder.Services.AddSingleton<CameraConnectionService>();
    builder.Services.AddHostedService(sp => sp.GetRequiredService<CameraConnectionService>());
    builder.Services.AddSingleton<ICameraPipelineHost>(sp => sp.GetRequiredService<CameraConnectionService>());

    // Periodic disk-retention enforcement; without this, continuous server
    // recording fills the disk and crashes the host.
    builder.Services.AddSingleton(new ServerMediaCleanupServiceOptions());
    builder.Services.AddHostedService<ServerMediaCleanupService>();

    // Clock-aligned segment rollover so server recordings don't grow into
    // single multi-day files. Uses the shared RecordingSlotCalculator so
    // boundary detection is immune to NTP rollback, midnight, and DST.
    builder.Services.AddSingleton(new ServerRecordingSegmentationServiceOptions());
    builder.Services.AddHostedService<ServerRecordingSegmentationService>();

    // Idle HLS stream reaper — sweeps orphaned FFmpeg transcoders every 10s.
    // Extracted from StreamingService's former internal Timer.
    builder.Services.AddSingleton(new HlsStreamReaperServiceOptions());
    builder.Services.AddHostedService<HlsStreamReaperService>();

    // Outbound webhooks: camera, recording and motion events are posted to
    // the receivers under Webhooks:Endpoints. Without any, the worker idles.
    var webhookOptions = builder.Configuration.GetSection(WebhookOptions.SectionName).Get<WebhookOptions>() ?? new WebhookOptions();
    builder.Services.AddSingleton(webhookOptions);
    builder.Services
        .AddHttpClient(WebhookSender.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(10));
    builder.Services.AddSingleton<IWebhookSender, WebhookSender>();

    // The receivers: those configured above, and those added through the
    // API, which are kept in webhooks.json in the data folder.
    builder.Services.AddSingleton(sp => new WebhookRegistry(
        sp.GetRequiredService<ServerDataPaths>().WebhooksPath,
        sp.GetRequiredService<WebhookOptions>(),
        sp.GetRequiredService<IWebhookSender>(),
        sp.GetRequiredService<SecretProtector>(),
        sp.GetRequiredService<ILogger<WebhookRegistry>>()));
    builder.Services.AddSingleton<IWebhookRegistry>(sp => sp.GetRequiredService<WebhookRegistry>());
    builder.Services.AddSingleton(new WebhookDispatcherServiceOptions());
    builder.Services.AddHostedService<WebhookDispatcherService>();

    // The users and their sessions (accounts.json in the data folder), and
    // the bearer handler that checks the server's own access tokens. While
    // the server has no user, setup-code.txt next to it creates the first admin.
    builder.Services.AddSingleton(TimeProvider.System);
    builder.Services.AddSingleton(sp => new AccountService(
        sp.GetRequiredService<ServerDataPaths>(),
        sp.GetRequiredService<TimeProvider>(),
        sp.GetRequiredService<ILogger<AccountService>>()));
    builder.Services.AddSingleton<IAccountService>(sp => sp.GetRequiredService<AccountService>());
    builder.Services.AddSingleton<IAuditLog>(sp => new AuditLog(
        sp.GetRequiredService<ServerDataPaths>().AuditLogPath,
        sp.GetRequiredService<TimeProvider>()));
    builder.Services.AddHttpContextAccessor();

    // The access token is the default scheme; the generated role policies name
    // both of the spec's schemes, so an API key passes a role check as well.
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer()
        .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(ApiKeyAuthenticationHandler.SchemeName, _ => { });
    builder.Services
        .AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
        .Configure<AccountService>((options, accounts) =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = accounts.Tokens.ValidationParameters;
        });

    // Periodic liveness beacon so a 4-day soak log shows steady process /
    // recording-state metrics rather than going silent between disruptions.
    builder.Services.AddSingleton(new ServerHeartbeatServiceOptions());
    builder.Services.AddHostedService<ServerHeartbeatService>();

    var app = builder.Build();

    // HTTPS when the server was given an HTTPS endpoint, outside development
    // only: under Aspire the clients also reach the plain "public" endpoint,
    // and an HTTP client drops the Authorization header on a redirect.
    if (HttpsEndpoints.IsConfigured(app.Configuration) && !app.Environment.IsDevelopment())
    {
        app.UseHsts();
        app.UseHttpsRedirection();
    }

    app.UseCors();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference();
    }

    // Redirect root to Scalar API docs
    app
        .MapGet("/", () => Results.Redirect("/scalar/v1"))
        .ExcludeFromDescription();

    // Every change a caller makes is written into the audit log (audit.log).
    app.UseMiddleware<AuditMiddleware>();

    // Map all generated REST endpoints, with a ProblemDetails for every error
    // that has no body of its own (an unknown address, a body that is no
    // JSON) and, outside Development, for an unhandled exception.
    app.MapVideoSurveillanceApi();

    // Liveness probe and recording diagnostics. Kept out of the OpenAPI
    // surface (ExcludeFromDescription) because they're operational
    // endpoints, not part of the API contract. /health/recordings is the
    // soak-monitoring surface — gives a single round-trip view of which
    // sessions are alive and which are "stuck" (session present but the
    // pipeline died and the reaper hasn't swept yet).
    // Liveness gate over the always-on background workers. Cleanup and
    // segmentation are intentionally excluded: cleanup may legitimately stop
    // (OnStartup run-once mode) and both no-op when disabled, so their state
    // isn't a process-health signal. A stale/stopped always-on worker flips
    // this to 503 so an orchestrator restarts the host.
    string[] monitoredServices =
    [
        nameof(CameraConnectionService),
        nameof(HlsStreamReaperService),
        nameof(ServerHeartbeatService),
    ];

    app
        .MapGet("/health", (IBackgroundServiceHealthService health) =>
        {
            var services = monitoredServices.ToDictionary(
                name => name,
                health.IsServiceRunning,
                StringComparer.Ordinal);

            var healthy = services.Values.All(running => running);

            var payload = new { Status = healthy ? "ok" : "degraded", Services = services };

            return healthy
                ? Results.Ok(payload)
                : Results.Json(payload, statusCode: StatusCodes.Status503ServiceUnavailable);
        })
        .ExcludeFromDescription();

    app
        .MapGet("/health/recordings", (ServerRecordingService recordingService) =>
        {
            var diagnostics = recordingService.GetDiagnostics();
            var stuck = diagnostics.Count(d => !d.IsPipelineActive);
            var startTime = System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime();
            var uptime = DateTime.UtcNow - startTime;

            return Results.Ok(new
            {
                UptimeSeconds = (long)uptime.TotalSeconds,
                ActiveRecordings = diagnostics.Count,
                StuckRecordings = stuck,
                DiskSpaceLow = diagnostics.Any(d => d.DiskSpaceLow),
                Sessions = diagnostics.Select(d => new
                {
                    d.CameraId,
                    d.CameraName,
                    d.FilePath,
                    StartedAtUtc = d.StartedAtUtc.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
                    DurationSeconds = (long)d.Duration.TotalSeconds,
                    d.IsPipelineActive,
                    d.DiskSpaceLow,
                }),
            });
        })
        .RequireAuthorization(policy => policy
            .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme, ApiKeyAuthenticationHandler.SchemeName)
            .RequireRole("admin"))
        .ExcludeFromDescription();

    // Serve HLS stream segments as static files, under the lease they are
    // watched on (/streams/{leaseId}/...): a lease that ended opens nothing.
    var hlsContentTypes = new Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider
    {
        Mappings =
        {
            [".m3u8"] = "application/vnd.apple.mpegurl",
            [".ts"] = "video/mp2t",
        },
    };

    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new LeaseStreamFileProvider(app.Services.GetRequiredService<StreamingService>()),
        RequestPath = "/streams",
        ContentTypeProvider = hlsContentTypes,
    });

    // Serve recordings and their thumbnails as static files, from the
    // recording folder that is configured at the time of the request.
    Directory.CreateDirectory(
        app.Services.GetRequiredService<IApplicationSettingsService>().Recording.RecordingPath);

    // Only for a signed-in caller, an API key, or the session's media token (?media=).
    app.UseWhen(
        context => context.Request.Path.StartsWithSegments("/recordings-files", StringComparison.OrdinalIgnoreCase),
        branch => branch.UseMiddleware<MediaAccessMiddleware>());

    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = app.Services.GetRequiredService<RecordingsFileProvider>(),
        RequestPath = "/recordings-files",
        ServeUnknownFileTypes = false,
        ContentTypeProvider = RecordingsFileProvider.ContentTypes,

        // "?download=1" sends the file as an attachment. A web page on
        // another address cannot make the browser save a file instead of
        // showing it; the browser only takes that from the answer itself.
        OnPrepareResponse = context =>
        {
            if (context.Context.Request.Query.ContainsKey("download"))
            {
                context.Context.Response.Headers.ContentDisposition =
                    new System.Net.Http.Headers.ContentDispositionHeaderValue("attachment")
                    {
                        FileNameStar = context.File.Name,
                    }.ToString();
            }
        },
    });

    // Bring the recording folder in line with the layout settings, in the
    // background: this catches a layout changed while the server was down and
    // files that were in use when the settings were saved. It does nothing
    // when everything is in place. The server names its files in UTC.
    _ = Task.Run(() =>
    {
        try
        {
            var settings = app.Services.GetRequiredService<IApplicationSettingsService>().Recording;
            var recordings = app.Services.GetRequiredService<IRecordingService>();
            if (!string.IsNullOrEmpty(settings.RecordingPath))
            {
                var result = RecordingLayoutReorganizer.Synchronize(
                    settings.RecordingPath,
                    RecordingLayoutOptions.From(settings),
                    [".mp4", ".mkv", ".avi"],
                    recordings.GetActiveSessions(),
                    recordings.SegmentRecording,
                    fileNamesAreUtc: true);

                if (result.MovedRecordings > 0 || result.MovedThumbnails > 0 || result.Errors.Count > 0)
                {
                    Log.Information(
                        "Recording folder brought in line with the layout settings - recordings moved: {Recordings}, thumbnails moved: {Thumbnails}, errors: {Errors}",
                        result.MovedRecordings,
                        result.MovedThumbnails,
                        result.Errors.Count);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not bring the recording folder in line with the layout settings");
        }
    });

    Log.Information("Video Surveillance API starting");

    await app
        .RunAsync()
        .ConfigureAwait(false);
}
catch (Exception ex)
{
    Log.Fatal(ex, "Video Surveillance API terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync().ConfigureAwait(false);
}

static AdvancedSettings LoadAdvancedSettingsForLogging(string settingsPath)
{
    if (!File.Exists(settingsPath))
    {
        return new AdvancedSettings();
    }

    try
    {
        var json = File.ReadAllText(settingsPath);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new AdvancedSettings();
        }

        var settings = JsonSerializer.Deserialize<ApplicationSettings>(
            json,
            Atc.Serialization.JsonSerializerOptionsFactory.Create());
        return settings?.Advanced ?? new AdvancedSettings();
    }
    catch (JsonException)
    {
        return new AdvancedSettings();
    }
    catch (IOException)
    {
        return new AdvancedSettings();
    }
}