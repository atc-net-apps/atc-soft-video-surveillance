namespace Atc.Video.Engine.Helpers;

/// <summary>
/// Decides how loud a failed open of a stream is logged. A camera out of reach is tried again
/// for as long as it is away, for days if need be: the first failure is a warning, the next ones
/// debug lines, and a warning again once per <c>reminder</c> while it lasts. The reason does not
/// start a new series: a camera out of reach answers "no answer within 15 s" one time and
/// "Connection timed out" the next.
/// Kept per source (the camera's name), across players, as a tile can open a new player for
/// each attempt.
/// </summary>
internal sealed class OpenFailureLog
{
    private readonly TimeProvider time;
    private readonly TimeSpan reminder;
    private readonly Dictionary<string, Series> failing = new(StringComparer.Ordinal);
    private readonly Lock gate = new();

    public OpenFailureLog(
        TimeProvider time,
        TimeSpan reminder)
    {
        this.time = time ?? throw new ArgumentNullException(nameof(time));
        this.reminder = reminder;
    }

    /// <summary>
    /// Gets the log every player shares.
    /// </summary>
    public static OpenFailureLog Shared { get; } = new(TimeProvider.System, TimeSpan.FromHours(1));

    /// <summary>
    /// Notes a failed open and tells how loud to log it.
    /// </summary>
    public OpenFailureNote Failed(string source)
    {
        var now = time.GetUtcNow();
        lock (gate)
        {
            if (!failing.TryGetValue(source, out var series))
            {
                failing[source] = new Series(now) { Attempts = 1, LastWarning = now };
                return new OpenFailureNote(Microsoft.Extensions.Logging.LogLevel.Warning, 1, TimeSpan.Zero);
            }

            series.Attempts++;
            if (now - series.LastWarning < reminder)
            {
                return new OpenFailureNote(Microsoft.Extensions.Logging.LogLevel.Debug, series.Attempts, now - series.First);
            }

            series.LastWarning = now;
            return new OpenFailureNote(Microsoft.Extensions.Logging.LogLevel.Warning, series.Attempts, now - series.First);
        }
    }

    /// <summary>
    /// Notes that the stream opened.
    /// </summary>
    /// <returns>The attempts that failed in a row before it, 0 when none did.</returns>
    public int Opened(string source)
    {
        lock (gate)
        {
            return failing.Remove(source, out var series) ? series.Attempts : 0;
        }
    }

    private sealed class Series(DateTimeOffset first)
    {
        public DateTimeOffset First { get; } = first;

        public int Attempts { get; set; }

        public DateTimeOffset LastWarning { get; set; }
    }
}