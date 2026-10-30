namespace AtcSoft.VideoSurveillance.Wpf.Core;

/// <summary>
/// A logger that keeps what is written, for a test to read.
/// </summary>
/// <typeparam name="T">The category the logger is for.</typeparam>
internal sealed class CollectingLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message)> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
        => null;

    public bool IsEnabled(LogLevel logLevel)
        => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);

        Entries.Add((logLevel, formatter(state, exception)));
    }
}