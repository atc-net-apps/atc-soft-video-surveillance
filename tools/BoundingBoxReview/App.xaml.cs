namespace BoundingBoxReview;

/// <summary>
/// The bounding box review tool. Started with a path (a recording or a review folder), it opens it.
/// </summary>
public partial class App
{
    protected override void OnStartup(StartupEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);

        // Atc.Wpf finds the texts only in loaded assemblies; the FFmpeg set-up dialog uses them.
        _ = AtcSoft.Video.Surveillance.Resources.Translations.ResourceManager;
        base.OnStartup(e);

        // A binding error is a bug: shown in the debugger's output.
        BindingErrorLogging.Start(static message => Debug.WriteLine($"Binding error: {message}"), showMessageBox: false);

        var viewModel = new MainWindowViewModel();
        var window = new MainWindow(viewModel);
        MainWindow = window;
        window.Show();

        if (e.Args.FirstOrDefault() is { } path)
        {
            _ = viewModel.OpenAsync(Path.GetFullPath(path));
        }
    }
}