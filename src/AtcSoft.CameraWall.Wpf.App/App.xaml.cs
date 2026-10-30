// ReSharper disable AsyncVoidEventHandlerMethod
namespace AtcSoft.CameraWall.Wpf.App;

public partial class CameraWallApp
{
    private readonly IHost host;
    private readonly ILogger<CameraWallApp> logger;

    public CameraWallApp()
    {
        // Load advanced settings early to configure logging before Host is built
        var advancedSettings = LoadAdvancedSettingsForLogging();

        // Configure Serilog based on settings. Drop framework Debug noise but
        // keep AtcSoft.* at Debug and Microsoft.* Information+ for ops.
        var loggerConfig = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .WriteTo.Debug(formatProvider: CultureInfo.InvariantCulture);

        if (advancedSettings.EnableDebugLogging)
        {
            Directory.CreateDirectory(advancedSettings.LogPath);

            var logFile = Path.Combine(advancedSettings.LogPath, "camera-wall-.log");
            loggerConfig.WriteTo.File(
                logFile,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
                formatProvider: CultureInfo.InvariantCulture);
        }

        // Under Aspire the logs (binding errors included) also go to the dashboard; the sink reads
        // the OTEL_EXPORTER_OTLP_* variables Aspire sets (endpoint, headers, protocol, service name).
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT")))
        {
            loggerConfig.WriteTo.OpenTelemetry();
        }

        Log.Logger = loggerConfig.CreateLogger();

        host = Host
            .CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices((_, services) =>
            {
                // Video engine services
                services.AddSingleton<IGpuAcceleratorFactory, D3D11AcceleratorFactory>();
                services.AddSingleton<IVideoPlayerFactory, VideoPlayerFactory>();

                // USB camera enumeration + hot-plug watcher (Windows-specific
                // Media Foundation + WMI implementation). The Null* fallbacks
                // are registered first so non-Windows hosts (future) still
                // compose; AddWindowsUsbCameraSupport replaces them.
                services.AddSingleton<IUsbCameraEnumerator>(NullUsbCameraEnumerator.Instance);
                services.AddSingleton<IUsbCameraWatcher, NullUsbCameraWatcher>();
                AtcSoft.VideoEngine.Windows.DependencyInjection.ServiceCollectionExtensions
                    .AddWindowsUsbCameraSupport(services);

                // Recording thumbnails (shared with the server)
                AtcSoft.VideoEngine.Windows.DependencyInjection.ServiceCollectionExtensions
                    .AddWindowsRecordingThumbnails(services);

                // Grey pictures for motion detection (shared with the server)
                AtcSoft.VideoEngine.Windows.DependencyInjection.ServiceCollectionExtensions
                    .AddWindowsMotionFrameReader(services);

                // Library services (auto-registered via [Registration] attribute)
                services.AddDependencyRegistrationsFromWpf(includeReferencedAssemblies: true);

                // Cleanup + segmentation each derive from Atc.Hosting's
                // BackgroundServiceBase. Register one singleton instance and
                // expose it both as its UI-facing interface and as an
                // IHostedService so the Generic Host drives the periodic work
                // and stops it gracefully on shutdown.
                services.AddSingleton(new MediaCleanupServiceOptions());
                services.AddSingleton(new DiskSpaceGuard(FreeSpaceReader.Default));
                services.AddSingleton<MediaCleanupService>();
                services.AddSingleton<IMediaCleanupService>(sp => sp.GetRequiredService<MediaCleanupService>());
                services.AddHostedService(sp => sp.GetRequiredService<MediaCleanupService>());

                services.AddSingleton<RecordingSegmentationService>();
                services.AddSingleton<IRecordingSegmentationService>(sp => sp.GetRequiredService<RecordingSegmentationService>());
                services.AddHostedService(sp => sp.GetRequiredService<RecordingSegmentationService>());

                // An activity index beside every recording, for the timelines (shared with the server).
                services.AddHostedService<ActivityIndexService>();

                // The preview pictures of each recording that ends, shown under the mouse on its timeline.
                services.AddSingleton<AtcSoft.CameraWall.Wpf.Services.RecordingPreviewService>();
                services.AddSingleton<IRecordingPreviews>(sp => sp.GetRequiredService<AtcSoft.CameraWall.Wpf.Services.RecordingPreviewService>());
                services.AddHostedService(sp => sp.GetRequiredService<AtcSoft.CameraWall.Wpf.Services.RecordingPreviewService>());

                // Toast notification service (used by library and app)
                services.AddSingleton<IToastNotificationService, ToastNotificationService>();

                // Connection-type detection (ONVIF) for the camera dialog's Detect
                // button and the implicit first-connect probe.
                services.AddSingleton<AtcSoft.VideoSurveillance.Services.ICameraConnectionTypeDetector>(
                    sp => new AtcSoft.VideoSurveillance.Services.OnvifConnectionTypeDetector(
                        sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<AtcSoft.VideoSurveillance.Services.OnvifConnectionTypeDetector>>()));

                // App
                services.AddSingleton<MainWindowViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();

        logger = host
            .Services
            .GetService<ILoggerFactory>()!
            .CreateLogger<CameraWallApp>();
    }

    private static AdvancedSettings LoadAdvancedSettingsForLogging()
    {
        // Same rule as the settings service applies later: a log folder that
        // still holds the shared default belongs in this app's own folder.
        var settings = new ApplicationSettings { Advanced = ReadAdvancedSettings() };
        _ = ApplicationSettingsService.UseOwnDataFolder(settings);
        return settings.Advanced;
    }

    private static AdvancedSettings ReadAdvancedSettings()
    {
        var settingsPath = ApplicationPaths.DefaultSettingsPath;

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
            // Invalid JSON content - use defaults
            return new AdvancedSettings();
        }
        catch (IOException)
        {
            // File access error - use defaults
            return new AdvancedSettings();
        }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        // Atc.Wpf finds the texts only in assemblies already loaded; load them before the first window binds to them.
        _ = Translations.ResourceManager;

        LogAppInitializing();

        // Hook on error before app really starts
        AppDomain.CurrentDomain.UnhandledException += CurrentDomainUnhandledException;
        Current.DispatcherUnhandledException += ApplicationOnDispatcherUnhandledException;
        base.OnStartup(e);

        // Binding errors always go to the log (and so to Aspire and the E2E smoke tests);
        // the message box is for a developer at the debugger.
        BindingErrorLogging.Start(
            static message => Log.ForContext(Serilog.Core.Constants.SourceContextPropertyName, "AtcSoft.BindingErrors")
                .Warning("Binding error: {BindingError}", message),
            showMessageBox: Debugger.IsAttached);
    }

    private void CurrentDomainUnhandledException(
        object sender,
        UnhandledExceptionEventArgs args)
    {
        if (args is not { ExceptionObject: Exception ex })
        {
            return;
        }

        var exceptionMessage = ex.GetMessage(true);

        LogCurrentDomainUnhandledException(ex, exceptionMessage);

        ShowFatalErrorDialog(exceptionMessage, Translations.CurrentDomainUnhandledException);
    }

    private void ApplicationOnDispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs args)
    {
        var exceptionMessage = args.Exception.GetMessage(true);
        if (exceptionMessage.Contains(
                "BindingExpression:Path=HorizontalContentAlignment; DataItem=null; target element is 'ComboBoxItem'",
                StringComparison.Ordinal))
        {
            args.Handled = true;
            return;
        }

        LogDispatcherUnhandledException(args.Exception, exceptionMessage);

        ShowFatalErrorDialog(exceptionMessage, Translations.DispatcherUnhandledException);

        args.Handled = true;
        Shutdown(-1);
    }

    /// <summary>
    /// Shows the fatal-error dialog using <see cref="InfoDialogBox"/>
    /// when an owner window is reachable. The handler runs while the
    /// app is collapsing — if no window is up yet (early-startup
    /// crash) we silently skip the popup; the original exception is
    /// already in the Serilog file sink.
    /// </summary>
    private static void ShowFatalErrorDialog(
        string message,
        string title)
    {
        var owner = Application.Current?.MainWindow
                    ?? Application.Current?.Windows.OfType<Window>().FirstOrDefault();
        if (owner is null)
        {
            return;
        }

        var settings = new DialogBoxSettings(DialogBoxType.Ok, LogCategoryType.Error)
        {
            TitleBarText = title,
            Width = 500,
        };

        var dialog = new InfoDialogBox(owner, settings, message);
        dialog.ShowDialog();
    }

    private async void ApplicationStartup(
        object sender,
        StartupEventArgs args)
    {
        LogAppStarting();

        // Show splash screen
        var splashScreen = new SplashScreenWindow
        {
            Header = Translations.ApplicationTitle,
            VersionText = $"Version {ApplicationHelper.GetVersion()}",
        };

        splashScreen.Show();

        // Load configuration (0%)
        AppHelper.RenderLoadingInitializeMessage(logger, Translations.InitializeLoadConfiguration, 0);

        // Start host (10%)
        AppHelper.RenderLoadingInitializeMessage(logger, Translations.InitializeStartHost, 10);
        await host.StartAsync();

        // Apply UI settings (30%)
        AppHelper.RenderLoadingInitializeMessage(logger, Translations.InitializeApplyUISettings, 30);
        var settingsService = host.Services.GetRequiredService<IApplicationSettingsService>();
        ApplyStartupSettings(settingsService.General);

        // Bring the recording folder in line with the layout settings, in the
        // background. This catches recordings that could not be moved when
        // the settings were saved (a file in use) and a layout that changed
        // while the app was not running. It does nothing when all is in place.
        _ = host.Services.GetService<IRecordingLayoutSynchronizer>()?.SynchronizeAsync();

        // Initialize camera engine (50%)
        AppHelper.RenderLoadingInitializeMessage(logger, Translations.InitializeCameraEngine, 50);
        var ffmpeg = FFmpegRuntime.Check();
        var ffmpegFolder = ffmpeg.Folder;
        if (ffmpeg.Status != FFmpegStatus.Ready)
        {
            // The FFmpeg the engine needs is not there: offer to install it, or a folder.
            LogFFmpegNotReady(ffmpeg.Describe());
            splashScreen.Hide();
            ffmpegFolder = FFmpegSetup.ShowDialog(ffmpeg);
            if (ffmpegFolder is null)
            {
                splashScreen.Close();
                Shutdown();
                return;
            }

            splashScreen.Show();
        }

        CameraWallEngine.Initialize(ffmpegFolder);

        // Load main window (80%)
        AppHelper.RenderLoadingInitializeMessage(logger, Translations.InitializeLoadMainWindow, 80);
        var mainWindow = host.Services.GetService<MainWindow>()!;

        // Apply window state from settings
        if (settingsService.General.StartMaximized)
        {
            mainWindow.WindowState = WindowState.Maximized;
        }

        // Media cleanup and recording segmentation are hosted background
        // services — the Generic Host started them in host.StartAsync() above,
        // so there's no explicit Initialize() here anymore.

        // Complete (100%)
        AppHelper.RenderLoadingInitializeMessage(logger, Translations.InitializeComplete, 100);

        // Show main window and close splash
        // Explicitly set MainWindow so dialogs resolve the correct Owner
        // (WPF auto-assigns MainWindow to the first Window created, which is the splash screen)
        mainWindow.Show();
        MainWindow = mainWindow;
        splashScreen.Close();

        LogAppStarted();

        _ = CheckForUpdatesAndNotifyAsync();
    }

    private async Task CheckForUpdatesAndNotifyAsync()
    {
        try
        {
            // Wait for UI to settle before checking
            await Task
                .Delay(3000)
                .ConfigureAwait(true);

            var manager = host.Services.GetRequiredService<ICameraWallManager>();
            await manager
                .CheckForUpdatesOnStartupAsync()
                .ConfigureAwait(true);

            if (!manager.IsUpdateAvailable)
            {
                return;
            }

            var toastService = host.Services.GetRequiredService<IToastNotificationService>();
            toastService.ShowInformation(
                Translations.UpdateAvailable,
                string.Format(
                    CultureInfo.CurrentCulture,
                    Translations.UpdateAvailableMessage1,
                    manager.UpdateVersion),
                useDesktop: true,
                expirationTime: TimeSpan.FromSeconds(10),
                onClick: manager.DownloadLatestUpdate);
        }
        catch
        {
            // Silently fail - startup notification is not critical
        }
    }

    private static void ApplyStartupSettings(GeneralSettings settings)
    {
        // Apply language
        if (NumberHelper.TryParseToInt(settings.Language, out var lcid))
        {
            var cultureInfo = new CultureInfo(lcid);
            CultureManager.SetCultures(cultureInfo.Name);
        }

        // Apply theme and accent
        ThemeManagerHelper.SetThemeAndAccent(
            Current,
            $"{settings.ThemeBase}.{settings.ThemeAccent}");
    }

    private async void ApplicationExit(
        object sender,
        ExitEventArgs args)
    {
        LogAppClosing();

        // Stop all active recordings to properly finalize recording files
        var recordingService = host.Services.GetService<IRecordingService>();
        if (recordingService is not null)
        {
            recordingService.StopAllRecordings();
            LogAllRecordingsStopped();
        }

        // Cleanup + segmentation are hosted services; host.StopAsync() below
        // stops them gracefully (their BackgroundServiceBase loops observe the
        // stopping token), so no explicit StopService() calls are needed.
        await host
            .StopAsync()
            .ConfigureAwait(false);

        host.Dispose();

        LogAppClosed();

        await Log.CloseAndFlushAsync().ConfigureAwait(false);
    }
}