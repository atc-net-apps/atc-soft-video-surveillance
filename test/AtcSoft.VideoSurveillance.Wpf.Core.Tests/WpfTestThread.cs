namespace AtcSoft.VideoSurveillance.Wpf.Core;

/// <summary>
/// The one UI thread that every test creating a window runs on.
/// The theming library binds itself to the first UI thread that uses it,
/// so windows created on a second thread fail to load.
/// </summary>
internal static class WpfTestThread
{
    private static readonly Lazy<System.Windows.Threading.Dispatcher> UiDispatcher = new(Start);

    /// <summary>
    /// Runs the action on the UI thread and returns the exception it threw, if any.
    /// </summary>
    public static Exception? Run(Action action)
    {
        Exception? error = null;
        UiDispatcher.Value.Invoke(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });

        return error;
    }

    private static System.Windows.Threading.Dispatcher Start()
    {
        System.Windows.Threading.Dispatcher? dispatcher = null;
        using var ready = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
            ready.Set();
            System.Windows.Threading.Dispatcher.Run();
        })
        {
            IsBackground = true,
        };

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait();
        return dispatcher!;
    }
}