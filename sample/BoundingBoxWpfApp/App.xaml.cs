namespace BoundingBoxWpfApp;

/// <summary>
/// The bounding box sample: a storybook or a recording through the real detector and overlay.
/// Logs go to <c>{data}\logs</c> (moved with <c>ATCSOFT_DATA_PATH</c>, as the E2E walk does), with
/// every binding error.
/// </summary>
public partial class App
{
    /// <summary>
    /// Gets the sample's data folder: the encoded storybooks and the logs.
    /// </summary>
    public static string DataFolder { get; } = ApplicationPaths.ResolveBaseDataPath(ApplicationPaths.DataPathOverride, "BoundingBoxSample");

    protected override void OnStartup(StartupEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        // Atc.Wpf finds the texts only in loaded assemblies; the FFmpeg set-up dialog uses them.
        _ = AtcSoft.VideoSurveillance.Resources.Translations.ResourceManager;

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                Path.Combine(DataFolder, "logs", "bounding-box-sample-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
                formatProvider: CultureInfo.InvariantCulture)
            .CreateLogger();

        AppDomain.CurrentDomain.UnhandledException += (_, args) => Log.Fatal(args.ExceptionObject as Exception, "Unhandled exception");
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        base.OnStartup(e);

        BindingErrorLogging.Start(
            static message => Log.ForContext(Serilog.Core.Constants.SourceContextPropertyName, "AtcSoft.BindingErrors")
                .Warning("Binding error: {BindingError}", message),
            showMessageBox: false);

        var options = StartupOptions.Parse(e.Args);
        Log.Information("Bounding box sample started ({Arguments})", string.Join(' ', e.Args));

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
        Log.Information("Bounding box sample stopped");
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private static void OnDispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs args)
    {
        Log.Fatal(args.Exception, "Unhandled exception on the UI thread");
        MessageBox.Show(args.Exception.Message, "Bounding box sample", MessageBoxButton.OK, MessageBoxImage.Error);
        args.Handled = true;
    }
}