namespace AtcSoft.Video.Surveillance.Tools.DetectionReview;

/// <summary>
/// Detection Review. Started with a path (a recording or a review folder), it opens it; with
/// <c>--every seconds</c>, a recording's pictures are that far apart. Logs go to
/// <c>{data}\logs</c> (moved with <c>ATCSOFT_DATA_PATH</c>, as the E2E walk does), with every
/// binding error.
/// </summary>
public partial class App
{
    /// <summary>
    /// Gets Detection Review's data folder: its logs. A review is kept beside its recording.
    /// </summary>
    public static string DataFolder { get; } = ApplicationPaths.ResolveBaseDataPath(ApplicationPaths.DataPathOverride, "DetectionReview");

    protected override void OnStartup(StartupEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        // --rerun <folder>: the detector over a review again and the comparison, without a window.
        if (e.Args is ["--rerun", { } rerunFolder])
        {
            Shutdown(Headless.Rerun(Path.GetFullPath(rerunFolder)));
            return;
        }

        // --masks <folder> [--no-ghosts]: what the detector saw at each picture (for tuning it).
        if (e.Args is ["--masks", { } masksFolder, .. var rest])
        {
            Shutdown(Headless.Masks(Path.GetFullPath(masksFolder), dissolveGhosts: !rest.Contains("--no-ghosts", StringComparer.Ordinal)));
            return;
        }

        // Atc.Wpf finds the texts only in loaded assemblies; the FFmpeg set-up dialog uses them.
        _ = AtcSoft.Video.Surveillance.Resources.Translations.ResourceManager;

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                Path.Combine(DataFolder, "logs", "detection-review-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
                formatProvider: CultureInfo.InvariantCulture)
            .CreateLogger();

        AppDomain.CurrentDomain.UnhandledException += (_, args) => Log.Fatal(args.ExceptionObject as Exception, "Unhandled exception");
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        base.OnStartup(e);

        // The tools are always dark with the blue accent.
        Atc.Wpf.Theming.Helpers.ThemeManagerHelper.SetThemeAndAccent(Current, "Dark.Blue");

        // A binding error is a bug: written to the log, where the E2E walk finds it.
        BindingErrorLogging.Start(
            static message => Log.ForContext(Serilog.Core.Constants.SourceContextPropertyName, "AtcSoft.BindingErrors")
                .Warning("Binding error: {BindingError}", message),
            showMessageBox: false);

        Log.Information("Detection Review started ({Arguments})", string.Join(' ', e.Args));

        var viewModel = new MainWindowViewModel();
        var window = new MainWindow(viewModel);
        MainWindow = window;
        window.Show();

        // A path, and --every seconds before or after it.
        var at = Array.IndexOf(e.Args, "--every");
        if (at >= 0 && at + 1 < e.Args.Length
            && double.TryParse(e.Args[at + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out var every) && every > 0)
        {
            viewModel.EverySeconds = every;
        }

        var path = e.Args.Where((_, i) => at < 0 || (i != at && i != at + 1)).FirstOrDefault();

        if (path is not null)
        {
            _ = viewModel.OpenAsync(Path.GetFullPath(path));
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("Detection Review stopped");
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private static void OnDispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs args)
    {
        Log.Fatal(args.Exception, "Unhandled exception on the UI thread");
        MessageBox.Show(args.Exception.Message, "Detection Review", MessageBoxButton.OK, MessageBoxImage.Error);
        args.Handled = true;
    }
}