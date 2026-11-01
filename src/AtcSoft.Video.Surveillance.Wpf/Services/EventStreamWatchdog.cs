namespace AtcSoft.Video.Surveillance.Wpf.Services;

/// <summary>
/// Keeps the event stream (GET /events) from waiting for ever on a connection
/// that died without a word: after a sleep, a NAT that forgot it, Wi-Fi that
/// came back with another address. The server sends a ping every
/// <see cref="HeartbeatSeconds"/> while nothing happens, so a stream with no
/// byte for <see cref="IdleTimeout"/> is taken as lost: its read fails as a
/// broken connection does, and the reader opens it again after the last event.
/// </summary>
public sealed class EventStreamWatchdog
{
    /// <summary>
    /// The seconds the client asks the server to wait before it sends a ping.
    /// </summary>
    public const int HeartbeatSeconds = 15;

    private readonly ConcurrentDictionary<IdleTimeoutStream, byte> streams = new();

    public EventStreamWatchdog(TimeProvider? time = null)
        => Time = time ?? TimeProvider.System;

    /// <summary>
    /// Gets how long a stream may be silent: three heartbeats.
    /// </summary>
    public static TimeSpan IdleTimeout { get; } = TimeSpan.FromSeconds(HeartbeatSeconds * 3);

    internal TimeProvider Time { get; }

    /// <summary>
    /// Ends every open event stream at once, so each is opened again at once:
    /// the computer woke up, or its network changed.
    /// </summary>
    public void AbortAll()
    {
        foreach (var stream in streams.Keys)
        {
            stream.Abort();
        }
    }

    /// <summary>
    /// Wraps a response's stream in one that fails when it is silent too long.
    /// </summary>
    internal Stream Watch(Stream inner)
    {
        var stream = new IdleTimeoutStream(inner, IdleTimeout, Time, this);
        streams.TryAdd(stream, 0);
        return stream;
    }

    internal void Forget(IdleTimeoutStream stream)
        => streams.TryRemove(stream, out _);
}