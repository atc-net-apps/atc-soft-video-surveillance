namespace AtcSoft.Video.Surveillance.CameraWall.Wpf.App;

/// <summary>
/// Adds the managed thread id to each log event, so a diagnostics log shows which thread
/// (the UI thread, a camera's stream, a timer) wrote what.
/// </summary>
internal sealed class ThreadIdEnricher : Serilog.Core.ILogEventEnricher
{
    public void Enrich(
        Serilog.Events.LogEvent logEvent,
        Serilog.Core.ILogEventPropertyFactory propertyFactory)
    {
        ArgumentNullException.ThrowIfNull(logEvent);
        ArgumentNullException.ThrowIfNull(propertyFactory);

        logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("ThreadId", Environment.CurrentManagedThreadId));
    }
}