namespace AtcSoft.Video.Surveillance.Tools.MotionLab;

/// <summary>
/// Motion Lab: a storybook or a recording through the real detector and overlay.
/// Logs go to <c>{data}\logs</c> (moved with <c>ATCSOFT_DATA_PATH</c>, as the E2E walk does), with
/// every binding error.
/// </summary>
public partial class App
{
    /// <summary>
    /// Gets Motion Lab's data folder: the encoded storybooks and the logs.
    /// </summary>
    public static string DataFolder { get; } = ApplicationPaths.ResolveBaseDataPath(ApplicationPaths.DataPathOverride, "MotionLab");

    protected override void OnStartup(StartupEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        // Atc.Wpf finds the texts only in loaded assemblies; the FFmpeg set-up dialog uses them.
        _ = AtcSoft.Video.Surveillance.Resources.Translations.ResourceManager;

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                Path.Combine(DataFolder, "logs", "motion-lab-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
                formatProvider: CultureInfo.InvariantCulture)
            .CreateLogger();

        AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        base.OnStartup(e);

        // The tools are always dark with the blue accent.
        Atc.Wpf.Theming.Helpers.ThemeManagerHelper.SetThemeAndAccent(Current, "Dark.Blue");

        BindingErrorLogging.Start(
            static message => Log.ForContext(Serilog.Core.Constants.SourceContextPropertyName, "AtcSoft.BindingErrors")
                .Warning("Binding error: {BindingError}", message),
            showMessageBox: false);

        var options = StartupOptions.Parse(e.Args);
        Log.Information("Motion Lab started ({Arguments})", string.Join(' ', e.Args));

        var viewModel = new MainWindowViewModel();
        var window = new MainWindow(viewModel);
        MainWindow = window;
        window.Show();

        if (options.StorybookPath is { } storybook)
        {
            _ = OpenAsync(viewModel, viewModel.OpenStorybookAsync(storybook, options.AnalysesPerSecond), options.LiveRotation);
        }
        else if (options.RecordingPath is { } recording)
        {
            _ = OpenAsync(viewModel, viewModel.OpenRecordingAsync(recording, options.AnalysesPerSecond), options.LiveRotation);
        }
        else if (options.SweepFolder is { } sweep)
        {
            _ = viewModel.SweepAsync(sweep, options.CorpusFolder ?? Environment.GetEnvironmentVariable(StartupOptions.CorpusVariable));
        }
    }

    // Opens a file, then plays it live when asked to.
    private static async Task OpenAsync(
        MainWindowViewModel viewModel,
        Task opening,
        int? liveRotation)
    {
        await opening.ConfigureAwait(true);
        if (liveRotation is { } rotation)
        {
            viewModel.LiveRotation = rotation;
            await viewModel.PlayLiveAsync().ConfigureAwait(true);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("Motion Lab stopped");
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    // The process ends, and the UI thread may be gone: the native message box, with the report saved
    // and copied.
    private static void OnCurrentDomainUnhandledException(
        object sender,
        UnhandledExceptionEventArgs args)
    {
        if (args is not { ExceptionObject: Exception ex })
        {
            return;
        }

        Log.Fatal(ex, "Unhandled exception");
        Log.CloseAndFlush();
        _ = Atc.Wpf.Dialogs.ExceptionReport.ShowMessageBox(ex, "Motion Lab");
    }

    // Shown with the full report (Details, Ctrl+C or the copy button copies it), then the tool closes.
    private void OnDispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs args)
    {
        if (Atc.Wpf.Dialogs.ExceptionReport.IsKnownHarmlessWpfBindingError(args.Exception))
        {
            args.Handled = true;
            return;
        }

        Log.Fatal(args.Exception, "Unhandled exception on the UI thread");
        Atc.Wpf.Components.Dialogs.ExceptionDialog.Show(args.Exception, "Motion Lab");

        args.Handled = true;
        Shutdown(-1);
    }
}