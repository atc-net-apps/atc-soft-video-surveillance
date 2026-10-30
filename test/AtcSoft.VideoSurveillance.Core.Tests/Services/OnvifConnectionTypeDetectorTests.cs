namespace AtcSoft.VideoSurveillance.Services;

public class OnvifConnectionTypeDetectorTests
{
    private const string DateTimeResponse = """
        <s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope" xmlns:tt="http://www.onvif.org/ver10/schema">
          <s:Body><GetSystemDateAndTimeResponse><SystemDateAndTime><tt:UTCDateTime>
            <tt:Time><tt:Hour>10</tt:Hour><tt:Minute>0</tt:Minute><tt:Second>0</tt:Second></tt:Time>
            <tt:Date><tt:Year>2026</tt:Year><tt:Month>1</tt:Month><tt:Day>2</tt:Day></tt:Date>
          </tt:UTCDateTime></SystemDateAndTime></GetSystemDateAndTimeResponse></s:Body>
        </s:Envelope>
        """;

    private const string WifiInterfacesResponse = """
        <s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope" xmlns:tt="http://www.onvif.org/ver10/schema">
          <s:Body><GetNetworkInterfacesResponse><NetworkInterfaces token="wlan0">
            <tt:Enabled>true</tt:Enabled>
            <tt:IPv4><tt:Config><tt:Manual><tt:Address>10.0.0.5</tt:Address></tt:Manual></tt:Config></tt:IPv4>
            <tt:Extension><tt:InterfaceType>71</tt:InterfaceType></tt:Extension>
          </NetworkInterfaces></GetNetworkInterfacesResponse></s:Body>
        </s:Envelope>
        """;

    [Fact]
    public async Task DetectAsync_Returns_Wifi_From_GetNetworkInterfaces()
    {
        using var harness = new Harness(request => Respond(request, WifiInterfacesResponse), [80]);

        var result = await harness.Detector.DetectAsync("10.0.0.5", "admin", "secret", TestContext.Current.CancellationToken);

        result.Should().Be(CameraConnectionType.Wifi);
    }

    [Fact]
    public async Task DetectAsync_Posts_Soap12_To_Device_Service()
    {
        using var harness = new Harness(request => Respond(request, WifiInterfacesResponse), [80]);

        await harness.Detector.DetectAsync("10.0.0.5", "admin", "secret", TestContext.Current.CancellationToken);

        harness.Handler.Requests.Should().HaveCount(2);
        var interfacesRequest = harness.Handler.Requests[1];
        interfacesRequest.Uri.Should().Be(new Uri("http://10.0.0.5:80/onvif/device_service"));
        interfacesRequest.ContentType.Should().StartWith("application/soap+xml");
        interfacesRequest.ContentType.Should().Contain("action=\"http://www.onvif.org/ver10/device/wsdl/GetNetworkInterfaces\"");
        interfacesRequest.Body.Should().Contain("UsernameToken").And.Contain("PasswordDigest");
    }

    [Fact]
    public async Task DetectAsync_Aligns_Token_Timestamp_To_Camera_Clock()
    {
        using var harness = new Harness(request => Respond(request, WifiInterfacesResponse), [80]);

        await harness.Detector.DetectAsync("10.0.0.5", "admin", "secret", TestContext.Current.CancellationToken);

        harness.Handler.Requests[1].Body.Should().Contain("2026-01-02T10:00:00Z");
    }

    [Fact]
    public async Task DetectAsync_Tries_Next_Port_When_First_Does_Not_Answer()
    {
        using var harness = new Harness(
            request => request.RequestUri!.Port == 80
                ? throw new HttpRequestException("refused")
                : Respond(request, WifiInterfacesResponse),
            [80, 2020]);

        var result = await harness.Detector.DetectAsync("10.0.0.5", "admin", "secret", TestContext.Current.CancellationToken);

        result.Should().Be(CameraConnectionType.Wifi);
        harness.Handler.Requests.Should().Contain(r => r.Uri.Port == 2020);
    }

    [Fact]
    public async Task DetectAsync_Reaches_The_Last_Port_When_The_Others_Never_Answer()
    {
        // A Tapo camera answers ONVIF on 2020 only. Connections to the other
        // ports hang until they time out, and three of those in a row used
        // to exhaust the whole time budget before 2020 was tried.
        using var harness = new Harness(
            async (request, cancellationToken) =>
            {
                if (request.RequestUri!.Port != 2020)
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }

                return Respond(request, WifiInterfacesResponse);
            },
            [80, 8080, 8000, 2020],
            requestTimeout: TimeSpan.FromMilliseconds(400),
            totalTimeout: TimeSpan.FromMilliseconds(1000));

        var result = await harness.Detector.DetectAsync("10.0.0.5", "admin", "secret", TestContext.Current.CancellationToken);

        result.Should().Be(CameraConnectionType.Wifi);
    }

    [Fact]
    public async Task DetectAsync_Sends_Credentials_Only_To_The_First_Port_That_Answers()
    {
        using var harness = new Harness(request => Respond(request, WifiInterfacesResponse), [80, 2020]);

        await harness.Detector.DetectAsync("10.0.0.5", "admin", "secret", TestContext.Current.CancellationToken);

        harness.Handler.Requests
            .Where(r => r.Body.Contains("UsernameToken", StringComparison.Ordinal))
            .Should().ContainSingle()
            .Which.Uri.Port.Should().Be(80);
    }

    [Fact]
    public async Task DetectAsync_Unauthorized_Returns_None_Without_Trying_Other_Ports()
    {
        using var harness = new Harness(
            request => request.Content!.ReadAsStringAsync().Result.Contains("GetNetworkInterfaces", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.Unauthorized)
                : Respond(request, DateTimeResponse),
            [80, 2020]);

        var result = await harness.Detector.DetectAsync("10.0.0.5", "admin", "wrong", TestContext.Current.CancellationToken);

        result.Should().Be(CameraConnectionType.None);
        harness.Handler.Requests
            .Where(r => r.Body.Contains("GetNetworkInterfaces", StringComparison.Ordinal))
            .Should().OnlyContain(r => r.Uri.Port == 80);
    }

    [Fact]
    public async Task DetectAsync_No_Onvif_Returns_None()
    {
        using var harness = new Harness(_ => new HttpResponseMessage(HttpStatusCode.NotFound), [80, 8080]);

        var result = await harness.Detector.DetectAsync("10.0.0.5", null, null, TestContext.Current.CancellationToken);

        result.Should().Be(CameraConnectionType.None);
    }

    [Fact]
    public async Task DetectAsync_Times_Out_Returns_None()
    {
        using var harness = new Harness(_ => throw new TaskCanceledException(), [80]);

        var result = await harness.Detector.DetectAsync("10.0.0.5", "admin", "secret", TestContext.Current.CancellationToken);

        result.Should().Be(CameraConnectionType.None);
    }

    [Fact]
    public async Task DetectAsync_Empty_Ip_Returns_None_Without_Requests()
    {
        using var harness = new Harness(request => Respond(request, WifiInterfacesResponse), [80]);

        var result = await harness.Detector.DetectAsync(string.Empty, "admin", "secret", TestContext.Current.CancellationToken);

        result.Should().Be(CameraConnectionType.None);
        harness.Handler.Requests.Should().BeEmpty();
    }

    [Fact]
    [SuppressMessage("Security", "CA5350:Do Not Use Weak Cryptographic Algorithms", Justification = "Verifies the SHA-1 digest mandated by WS-Security.")]
    public void ComputePasswordDigest_Hashes_Raw_Nonce_Bytes()
    {
        var nonce = new byte[] { 1, 2, 3, 4 };
        const string created = "2026-01-02T10:00:00Z";
        const string password = "secret";

        var expected = Convert.ToBase64String(SHA1.HashData(
            [.. nonce, .. Encoding.UTF8.GetBytes(created), .. Encoding.UTF8.GetBytes(password)]));

        OnvifConnectionTypeDetector.ComputePasswordDigest(nonce, created, password).Should().Be(expected);
    }

    [Fact]
    public void BuildEnvelope_Without_User_Has_No_Security_Header()
    {
        var envelope = OnvifConnectionTypeDetector.BuildEnvelope("GetSystemDateAndTime", null, null, DateTimeOffset.UtcNow, [1, 2, 3]);

        envelope.Should().Contain("GetSystemDateAndTime").And.NotContain("UsernameToken");
    }

    [Fact]
    public void BuildEnvelope_Escapes_User_Name()
    {
        var envelope = OnvifConnectionTypeDetector.BuildEnvelope("GetNetworkInterfaces", "a<b>&c", "p", DateTimeOffset.UtcNow, [1, 2, 3]);

        envelope.Should().Contain("a&lt;b&gt;&amp;c");
    }

    private static HttpResponseMessage Respond(
        HttpRequestMessage request,
        string interfacesResponse)
    {
        var body = request.Content!.ReadAsStringAsync().Result;
        var content = body.Contains("GetSystemDateAndTime", StringComparison.Ordinal)
            ? DateTimeResponse
            : interfacesResponse;

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/soap+xml"),
        };
    }

    /// <summary>
    /// Owns the fake handler, the HttpClient and the detector under test.
    /// </summary>
    private sealed class Harness : IDisposable
    {
        private readonly HttpClient client;

        public Harness(
            Func<HttpRequestMessage, HttpResponseMessage> respond,
            IReadOnlyList<int> ports)
            : this((request, _) => Task.FromResult(respond(request)), ports)
        {
        }

        public Harness(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond,
            IReadOnlyList<int> ports,
            TimeSpan? requestTimeout = null,
            TimeSpan? totalTimeout = null)
        {
            Handler = new FakeHandler(respond);
            client = new HttpClient(Handler, disposeHandler: false);
            Detector = new OnvifConnectionTypeDetector(
                client,
                new OnvifConnectionTypeDetectorOptions
                {
                    Ports = ports,
                    RequestTimeout = requestTimeout ?? TimeSpan.FromSeconds(5),
                    TotalTimeout = totalTimeout ?? TimeSpan.FromSeconds(10),
                });
        }

        public FakeHandler Handler { get; }

        public OnvifConnectionTypeDetector Detector { get; }

        public void Dispose()
        {
            client.Dispose();
            Handler.Dispose();
        }
    }

    private sealed record CapturedRequest(
        Uri Uri,
        string ContentType,
        string Body);

    private sealed class FakeHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
        : HttpMessageHandler
    {
        private readonly Lock requestsLock = new();
        private readonly List<CapturedRequest> requests = [];

        /// <summary>
        /// Gets a snapshot of the requests seen so far. Ports are probed
        /// concurrently, so the list is guarded.
        /// </summary>
        public IReadOnlyList<CapturedRequest> Requests
        {
            get
            {
                lock (requestsLock)
                {
                    return [.. requests];
                }
            }
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            lock (requestsLock)
            {
                requests.Add(new CapturedRequest(
                    request.RequestUri!,
                    request.Content.Headers.ContentType?.ToString() ?? string.Empty,
                    body));
            }

            return await respond(request, cancellationToken);
        }
    }
}