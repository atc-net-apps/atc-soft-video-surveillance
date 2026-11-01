namespace AtcSoft.Video.Surveillance.Wpf.Services;

/// <summary>
/// The client's connections, with TCP keep-alive, reach a real server.
/// </summary>
public sealed class KeepAliveHttpHandlerTests
{
    [Fact]
    public async Task A_Call_Over_A_Keep_Alive_Connection_Is_Answered()
    {
        using var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var serving = ServeOnceAsync(listener);
        using var handler = KeepAliveHttpHandler.Create();
        using var client = new HttpClient(handler, disposeHandler: false);

        using var response = await client.GetAsync(new Uri($"http://127.0.0.1:{port}/health"), TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        await serving;
    }

    [Fact]
    public void A_Socket_Gets_Keep_Alive()
    {
        using var socket = new System.Net.Sockets.Socket(System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);

        KeepAliveHttpHandler.Configure(socket);

        socket.GetSocketOption(System.Net.Sockets.SocketOptionLevel.Socket, System.Net.Sockets.SocketOptionName.KeepAlive).Should().NotBe(0);
        socket.GetSocketOption(System.Net.Sockets.SocketOptionLevel.Tcp, System.Net.Sockets.SocketOptionName.TcpKeepAliveTime).Should().Be((int)KeepAliveHttpHandler.KeepAliveTime.TotalSeconds);
    }

    private static async Task ServeOnceAsync(
        System.Net.Sockets.TcpListener listener)
    {
        using var connection = await listener.AcceptTcpClientAsync(TestContext.Current.CancellationToken).ConfigureAwait(false);
        var stream = connection.GetStream();
        var buffer = new byte[4096];
        var request = new System.Text.StringBuilder();
        while (!request.ToString().Contains("\r\n\r\n", StringComparison.Ordinal))
        {
            var read = await stream.ReadAsync(buffer, TestContext.Current.CancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return;
            }

            request.Append(System.Text.Encoding.ASCII.GetString(buffer, 0, read));
        }

        await stream.WriteAsync("HTTP/1.1 200 OK\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"u8.ToArray(), TestContext.Current.CancellationToken).ConfigureAwait(false);
    }
}