namespace AtcSoft.Video.Surveillance.Api.Host;

/// <summary>
/// Reading a camera's streams over ONVIF, the whole way through: the server asks a fake camera on
/// this machine, which answers as an ONVIF camera does.
/// </summary>
[Collection(ApiHostTestGroup.Name)]
public sealed class ApiHostOnvifTests(ApiHostFixture host)
{
    [Fact]
    public async Task The_Camera_Streams_Are_Read_And_The_Sub_Stream_Proposed()
    {
        using var camera = new FakeOnvifCamera();
        using var client = host.CreateClient();

        var answer = await client.PostAsJsonAsync(
            "/onvif/profiles",
            new { address = "127.0.0.1", port = camera.Port, userName = "admin", password = "secret" },
            TestContext.Current.CancellationToken);

        answer.StatusCode.Should().Be(HttpStatusCode.OK, await answer.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var result = await answer.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        result.GetProperty("outcome").GetString().Should().Be("read");
        result.GetProperty("profiles").GetArrayLength().Should().Be(2);
        (result.GetProperty("port").GetInt32(), result.GetProperty("path").GetString()).Should().Be((554, "/Streaming/Channels/101"));
        result.GetProperty("subStreamPath").GetString().Should().Be("/Streaming/Channels/102");
        camera.Bodies.Should().OnlyContain(b => !b.Contains("secret", StringComparison.Ordinal), "the password is sent as a digest only");
    }

    [Fact]
    public async Task A_Viewer_May_Not_Read_A_Camera()
    {
        using var admin = host.CreateClient();
        var name = "viewer-" + Guid.NewGuid().ToString("N")[..8];
        (await admin.PostAsJsonAsync("/users", new { userName = name, password = "a password here", role = "viewer" }, TestContext.Current.CancellationToken))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        var (_, session) = await host.SignInAsync(name, "a password here");
        using var viewer = host.CreateAnonymousClient();
        viewer.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", session.GetProperty("accessToken").GetString());

        var answer = await viewer.PostAsJsonAsync("/onvif/profiles", new { address = "127.0.0.1", port = 1 }, TestContext.Current.CancellationToken);
        var search = await viewer.PostAsync(new Uri("/onvif/discover", UriKind.Relative), content: null, TestContext.Current.CancellationToken);

        answer.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        search.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task The_Twenty_First_Read_In_A_Minute_Is_Turned_Away()
    {
        var address = IPAddress.Parse("192.0.2.71");
        var token = host.GetAdminAccessToken();

        // All at once, as in the snapshot limit's test: a busy machine is not mistaken for a new window.
        var calls = await Task.WhenAll(Enumerable.Range(0, 21).Select(_ => host.SendAsync(context =>
        {
            context.Request.Method = HttpMethods.Post;
            context.Request.Path = "/onvif/profiles";
            context.Request.ContentType = "application/json";
            context.Request.Body = new MemoryStream("""{"address":"no address!"}"""u8.ToArray());
            context.Request.Headers.Authorization = "Bearer " + token;
            context.Connection.RemoteIpAddress = address;
        })));

        Array.ConvertAll(calls, call => call.Response.StatusCode)
            .Count(status => status == StatusCodes.Status429TooManyRequests).Should().Be(1);
    }

    [Fact]
    public async Task A_Search_Answers_With_What_Was_Found()
    {
        // Whatever this machine's networks hold: a CI runner has no cameras, a test PC may have some.
        using var client = host.CreateClient();

        var answer = await client.PostAsync(new Uri("/onvif/discover", UriKind.Relative), content: null, TestContext.Current.CancellationToken);

        answer.StatusCode.Should().Be(HttpStatusCode.OK, await answer.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        var result = await answer.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        result.GetProperty("devices").ValueKind.Should().Be(JsonValueKind.Array);
        result.GetProperty("couldProbe").ValueKind.Should().BeOneOf(JsonValueKind.True, JsonValueKind.False);
    }

    [Fact]
    public async Task The_Seventh_Search_In_A_Minute_Is_Turned_Away()
    {
        var address = IPAddress.Parse("192.0.2.72");
        var token = host.GetAdminAccessToken();

        var calls = await Task.WhenAll(Enumerable.Range(0, 7).Select(_ => host.SendAsync(context =>
        {
            context.Request.Method = HttpMethods.Post;
            context.Request.Path = "/onvif/discover";
            context.Request.Headers.Authorization = "Bearer " + token;
            context.Connection.RemoteIpAddress = address;
        })));

        Array.ConvertAll(calls, call => call.Response.StatusCode)
            .Count(status => status == StatusCodes.Status429TooManyRequests).Should().Be(1);
    }

    /// <summary>
    /// An ONVIF camera on this machine: plain HTTP over a socket, one request per connection, each
    /// answered by its SOAP action.
    /// </summary>
    private sealed class FakeOnvifCamera : IDisposable
    {
        private readonly System.Net.Sockets.TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource stopping = new();
        private readonly Task serving;

        public FakeOnvifCamera()
        {
            listener.Start();
            serving = Task.Run(ServeAsync, CancellationToken.None);
        }

        public int Port => ((IPEndPoint)listener.LocalEndpoint).Port;

        public System.Collections.Concurrent.ConcurrentBag<string> Bodies { get; } = [];

        public void Dispose()
        {
            stopping.Cancel();
            listener.Stop();
            try
            {
                serving.Wait(TimeSpan.FromSeconds(5), CancellationToken.None);
            }
            catch (AggregateException)
            {
                // Stopped while accepting.
            }

            listener.Dispose();
            stopping.Dispose();
        }

        private async Task ServeAsync()
        {
            while (!stopping.IsCancellationRequested)
            {
                System.Net.Sockets.TcpClient connection;
                try
                {
                    connection = await listener.AcceptTcpClientAsync(stopping.Token);
                }
                catch (Exception ex) when (ex is OperationCanceledException or System.Net.Sockets.SocketException or ObjectDisposedException)
                {
                    return;
                }

                _ = Task.Run(() => AnswerAsync(connection), CancellationToken.None);
            }
        }

        private async Task AnswerAsync(System.Net.Sockets.TcpClient connection)
        {
            using (connection)
            {
                var stream = connection.GetStream();
                using var reader = new StreamReader(stream, System.Text.Encoding.UTF8, leaveOpen: true);
                var action = string.Empty;
                var length = 0;
                string? line;
                while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync(stopping.Token)))
                {
                    if (line.StartsWith("Content-Type:", StringComparison.OrdinalIgnoreCase))
                    {
                        action = line[(line.IndexOf("action=\"", StringComparison.Ordinal) + 8)..].TrimEnd('"');
                    }
                    else if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                    {
                        length = int.Parse(line[15..].Trim(), CultureInfo.InvariantCulture);
                    }
                }

                var buffer = new char[length];
                var read = 0;
                while (read < length && await reader.ReadAsync(buffer.AsMemory(read, length - read), stopping.Token) is var n and > 0)
                {
                    read += n;
                }

                var body = new string(buffer, 0, read);
                Bodies.Add(body);
                var answer = System.Text.Encoding.UTF8.GetBytes(AnswerTo(action[(action.LastIndexOf('/', StringComparison.Ordinal) + 1)..], body));
                var head = System.Text.Encoding.ASCII.GetBytes(
                    $"HTTP/1.1 200 OK\r\nContent-Type: application/soap+xml; charset=utf-8\r\nContent-Length: {answer.Length}\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(head, stopping.Token);
                await stream.WriteAsync(answer, stopping.Token);
            }
        }

        private static string AnswerTo(
            string operation,
            string body)
            => operation switch
            {
                "GetSystemDateAndTime" => Envelope("<tds:GetSystemDateAndTimeResponse><tds:SystemDateAndTime/></tds:GetSystemDateAndTimeResponse>"),
                "GetCapabilities" => Envelope("<tds:GetCapabilitiesResponse><tds:Capabilities><tt:Media><tt:XAddr>http://10.9.9.9/onvif/Media</tt:XAddr></tt:Media></tds:Capabilities></tds:GetCapabilitiesResponse>"),
                "GetProfiles" => Envelope(
                    "<trt:GetProfilesResponse>" +
                    Profile("101", 1920, 1080) +
                    Profile("102", 640, 360) +
                    "</trt:GetProfilesResponse>"),
                "GetStreamUri" => Envelope(
                    "<trt:GetStreamUriResponse><trt:MediaUri><tt:Uri>rtsp://10.9.9.9:554/Streaming/Channels/" +
                    System.Xml.Linq.XDocument.Parse(body).Descendants().Single(e => e.Name.LocalName == "ProfileToken").Value +
                    "</tt:Uri></trt:MediaUri></trt:GetStreamUriResponse>"),
                _ => Envelope(string.Empty),
            };

        private static string Profile(
            string token,
            int width,
            int height)
            => $"<trt:Profiles token=\"{token}\"><tt:Name>Profile {token}</tt:Name><tt:VideoEncoderConfiguration><tt:Encoding>H264</tt:Encoding>" +
               $"<tt:Resolution><tt:Width>{width}</tt:Width><tt:Height>{height}</tt:Height></tt:Resolution></tt:VideoEncoderConfiguration></trt:Profiles>";

        private static string Envelope(string body)
            => "<s:Envelope xmlns:s=\"http://www.w3.org/2003/05/soap-envelope\" xmlns:tds=\"http://www.onvif.org/ver10/device/wsdl\" " +
               "xmlns:trt=\"http://www.onvif.org/ver10/media/wsdl\" xmlns:tt=\"http://www.onvif.org/ver10/schema\"><s:Body>" + body + "</s:Body></s:Envelope>";
    }
}