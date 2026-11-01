namespace AtcSoft.Video.Surveillance.Wpf.Services;

/// <summary>
/// The client's connections to the server, with TCP keep-alive: a connection
/// that died without a word (a sleep, a NAT that forgot it) is noticed by the
/// operating system within about a minute, instead of hours.
/// </summary>
public static class KeepAliveHttpHandler
{
    /// <summary>
    /// Gets how long a connection is idle before the first keep-alive probe.
    /// </summary>
    public static TimeSpan KeepAliveTime { get; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets the time between probes that are not answered.
    /// </summary>
    public static TimeSpan KeepAliveInterval { get; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets how many probes may go unanswered before the connection is dropped.
    /// </summary>
    public static int KeepAliveRetryCount { get; } = 3;

    /// <summary>
    /// Creates the handler that opens the connections.
    /// </summary>
    public static SocketsHttpHandler Create()
        => new()
        {
            ConnectCallback = ConnectAsync,

            // A pooled connection is used again for a while only: a server that
            // moved to another address is found again.
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        };

    internal static void Configure(Socket socket)
    {
        ArgumentNullException.ThrowIfNull(socket);

        socket.NoDelay = true;
        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, optionValue: true);
        socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, (int)KeepAliveTime.TotalSeconds);
        socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, (int)KeepAliveInterval.TotalSeconds);
        socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, KeepAliveRetryCount);
    }

    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The socket is owned by the NetworkStream returned, or disposed when the connection fails.")]
    private static async ValueTask<Stream> ConnectAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken)
    {
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
        try
        {
            Configure(socket);
            await socket.ConnectAsync(context.DnsEndPoint, cancellationToken).ConfigureAwait(false);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}