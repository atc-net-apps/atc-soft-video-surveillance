namespace AtcSoft.VideoSurveillance.Wpf.Core.Diagnostics;

/// <summary>
/// Writes every WPF data-binding error to the log, with or without a debugger, so the
/// log file, the Aspire dashboard and the E2E smoke tests see them.
/// </summary>
public static class BindingErrorLogging
{
    private static Action<string>? write;

    /// <summary>
    /// Starts tracing binding errors. Call once, early in OnStartup.
    /// </summary>
    /// <param name="writeBindingError">Writes one binding error to the log; the apps log it at Warning.</param>
    /// <param name="showMessageBox">Also show each error in a message box, for a developer at the debugger.</param>
    public static void Start(
        Action<string> writeBindingError,
        bool showMessageBox)
    {
        ArgumentNullException.ThrowIfNull(writeBindingError);

        if (write is null)
        {
            BindingErrorTraceListener.BindingErrorOccurred += OnBindingError;
        }

        write = writeBindingError;

        // WPF only traces bindings with a debugger attached unless the trace sources
        // have been refreshed; Refresh switches them on for this process.
        PresentationTraceSources.Refresh();
        BindingErrorTraceListener.StartTrace(SourceLevels.Error, TraceOptions.None, showMessageBox);
    }

    private static void OnBindingError(
        object? sender,
        BindingErrorEventArgs e)
        => write?.Invoke(e.Message);
}