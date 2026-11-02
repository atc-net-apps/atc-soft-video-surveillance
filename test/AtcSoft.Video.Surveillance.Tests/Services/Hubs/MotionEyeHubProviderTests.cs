namespace AtcSoft.Video.Surveillance.Services.Hubs;

/// <summary>
/// The cameras of a motionEye (0.44 and later, with a session) or a motionEyeOS (an older
/// motionEye, whose requests are signed): listed, with their MJPEG streams, and made into cameras.
/// </summary>
public sealed class MotionEyeHubProviderTests
{
    private const string Cameras = """
        {"cameras": [
          {"id": 1, "name": "Garage", "enabled": true, "proto": "v4l2", "video_streaming": true, "streaming_port": 8081,
           "streaming_auth_mode": "basic", "streaming_username": "viewer", "streaming_password": "pw", "resolution": "1280x720", "streaming_framerate": 5},
          {"id": 2, "name": "Pi camera", "enabled": true, "proto": "mmal", "video_streaming": true, "streaming_port": 8082, "streaming_auth_mode": "disabled"},
          {"id": 3, "name": "Off", "enabled": false, "proto": "v4l2", "video_streaming": true, "streaming_port": 8083},
          {"id": 4, "name": "Local only", "enabled": true, "proto": "v4l2", "video_streaming": false, "streaming_port": 8084},
          {"id": 5, "name": "Door", "enabled": true, "proto": "mjpeg", "url": "http://user:secret@192.168.1.60/video.mjpg"},
          {"id": 6, "name": "Elsewhere", "enabled": true, "proto": "motioneye"}
        ]}
        """;

    private static readonly Uri Hub = new("http://pi-garage:8765/");

    [Theory]
    [InlineData("pi-garage", "http://pi-garage:8765/")]
    [InlineData("192.168.1.50", "http://192.168.1.50:8765/")]
    [InlineData("pi-garage:9000", "http://pi-garage:9000/")]
    [InlineData("http://pi-garage", "http://pi-garage/")]
    [InlineData("https://cams.example.com/motioneye", "https://cams.example.com/motioneye/")]
    public void What_A_User_Types_Is_The_Hubs_Address(
        string typed,
        string expected)
        => MotionEyeHubProvider.ToHubAddress(typed).Should().Be(new Uri(expected));

    [Theory]
    [InlineData("")]
    [InlineData("ftp://pi-garage")]
    public void Something_Else_Is_No_Address(string typed)
        => MotionEyeHubProvider.ToHubAddress(typed).Should().BeNull();

    // The expected values come from motionEye 0.42's own compute_signature, run in Python.
    [Theory]
    [InlineData("/config/list?_username=admin", "secret", "b9c9cedbb7aba1e885a02179f7ee714959353b01")]
    [InlineData("/picture/1/current?b=x y&_username=admin&a=1&_signature=zzz", "p@ss wörd", "db717fc5a8bc80b26336bc16b5e7907ff99c1e6a")]
    [InlineData("/config/list?_username=admin", "", "c6d42c8b27f0160590091a96cd3d421e69b47f23")]
    public void An_Older_motionEyes_Request_Is_Signed_As_It_Signs_Them(
        string pathAndQuery,
        string password,
        string expected)
        => MotionEyeHubProvider.Sign("GET", pathAndQuery, body: null, password).Should().Be(expected);

    [Fact]
    public void A_USB_Camera_Is_Played_From_Its_Streaming_Port_With_Its_Streaming_User()
    {
        var garage = MotionEyeHubProvider.ReadCameras(Hub, Cameras)!.Single(c => c.Id == "1");

        garage.Should().Be(new HubCamera("1", "Garage", new Uri("http://pi-garage:8081/"), HubStreamKind.Mjpeg, "viewer", "pw", "1280x720", 5, IsUsb: true, HubCameraProblem.None));
    }

    [Fact]
    public void What_Cannot_Be_Played_Says_Why()
    {
        var cameras = MotionEyeHubProvider.ReadCameras(Hub, Cameras)!.ToDictionary(c => c.Id, StringComparer.Ordinal);

        cameras["2"].Problem.Should().Be(HubCameraProblem.None);
        cameras["2"].IsUsb.Should().BeFalse("a Raspberry Pi camera module is not a USB camera");
        cameras["2"].StreamUserName.Should().BeNull("its stream needs no sign-in");
        cameras["3"].Problem.Should().Be(HubCameraProblem.Disabled);
        cameras["4"].Problem.Should().Be(HubCameraProblem.StreamingOff);
        cameras["6"].Problem.Should().Be(HubCameraProblem.OnAnotherHub);
    }

    [Fact]
    public void A_Simple_MJPEG_Camera_Is_Its_Own_Stream_Without_Its_Credentials()
        => MotionEyeHubProvider.ReadCameras(Hub, Cameras)!.Single(c => c.Id == "5").Stream.Should().Be(new Uri("http://192.168.1.60/video.mjpg"));

    [Fact]
    public void Something_That_Is_Not_The_List_Is_No_List()
        => MotionEyeHubProvider.ReadCameras(Hub, "<html></html>").Should().BeNull();

    [Fact]
    public async Task A_Current_motionEye_Is_Signed_In_To_And_Listed_With_Its_Session()
    {
        using var hub = new FakeMotionEye(older: false);
        using var client = new HttpClient(hub, disposeHandler: false);
        var sut = new MotionEyeHubProvider(client);

        var listing = await sut.ListAsync("pi-garage", "admin", "secret", TestContext.Current.CancellationToken);

        listing.Outcome.Should().Be(CameraHubOutcome.Listed);
        listing.OlderSignIn.Should().BeFalse();
        listing.Cameras.Should().HaveCount(6);
        hub.ListRequest!.Headers.GetValues("Cookie").Should().Equal("user=session-1");
    }

    [Fact]
    public async Task A_motionEyeOS_Is_Listed_With_Signed_Requests_And_Says_Its_Sign_In_Is_Older()
    {
        using var hub = new FakeMotionEye(older: true);
        using var client = new HttpClient(hub, disposeHandler: false);
        var sut = new MotionEyeHubProvider(client);

        var listing = await sut.ListAsync("pi-garage", "admin", "secret", TestContext.Current.CancellationToken);

        listing.Outcome.Should().Be(CameraHubOutcome.Listed);
        listing.OlderSignIn.Should().BeTrue();
        hub.ListRequest!.RequestUri!.Query.Should().Be("?_username=admin&_signature=b9c9cedbb7aba1e885a02179f7ee714959353b01");
    }

    [Fact]
    public async Task A_Wrong_Password_Is_Refused()
    {
        using var hub = new FakeMotionEye(older: false) { Password = "other" };
        using var client = new HttpClient(hub, disposeHandler: false);
        var sut = new MotionEyeHubProvider(client);

        (await sut.ListAsync("pi-garage", "admin", "secret", TestContext.Current.CancellationToken)).Outcome.Should().Be(CameraHubOutcome.SignInRefused);
    }

    [Fact]
    public async Task Another_Web_Server_Is_Not_A_motionEye()
    {
        using var hub = new FakeMotionEye(older: true) { Page = "<html>a router</html>" };
        using var client = new HttpClient(hub, disposeHandler: false);
        var sut = new MotionEyeHubProvider(client);

        (await sut.ListAsync("router", "admin", "secret", TestContext.Current.CancellationToken)).Outcome.Should().Be(CameraHubOutcome.NotThisKind);
    }

    [Fact]
    public async Task Nothing_At_The_Address_Is_Unreachable()
    {
        using var hub = new FakeMotionEye(older: false) { Down = true };
        using var client = new HttpClient(hub, disposeHandler: false);
        var sut = new MotionEyeHubProvider(client);

        (await sut.ListAsync("pi-garage", "admin", "secret", TestContext.Current.CancellationToken)).Outcome.Should().Be(CameraHubOutcome.Unreachable);
    }

    [Fact]
    public async Task A_Cameras_Picture_Is_Fetched_With_The_Session()
    {
        using var hub = new FakeMotionEye(older: false);
        using var client = new HttpClient(hub, disposeHandler: false);
        var sut = new MotionEyeHubProvider(client);

        var picture = await sut.GetSnapshotAsync(Hub, "admin", "secret", "1", TestContext.Current.CancellationToken);

        picture.Should().Equal(0xFF, 0xD8, 0xFF);
    }

    [Fact]
    public void A_Hub_Camera_Becomes_A_Network_Camera_On_Its_Stream_With_Its_Origin()
    {
        var listing = new CameraHubListing(CameraHubOutcome.Listed, CameraHubKinds.MotionEye, Hub, MotionEyeHubProvider.ReadCameras(Hub, Cameras)!);
        var camera = new CameraConfiguration();

        HubCameraSetup.ApplyTo(camera, listing, listing.Cameras[0]).Should().BeTrue();

        camera.BuildUri().Should().Be(new Uri("http://viewer:pw@pi-garage:8081/"));
        camera.Display.DisplayName.Should().Be("Garage");
        camera.Connection.Hub!.Address.Should().Be("http://pi-garage:8765/");
        camera.Connection.Hub.CameraId.Should().Be("1");
        camera.Overrides.Recording.TranscodeVideoCodec.Should().Be(VideoTranscodeCodec.H264, "MJPEG is recorded as H.264");
    }

    [Fact]
    public void A_Hub_Camera_That_Cannot_Be_Played_Is_Not_Made_A_Camera()
    {
        var listing = new CameraHubListing(CameraHubOutcome.Listed, CameraHubKinds.MotionEye, Hub, MotionEyeHubProvider.ReadCameras(Hub, Cameras)!);

        HubCameraSetup.ApplyTo(new CameraConfiguration(), listing, listing.Cameras.Single(c => c.Id == "4")).Should().BeFalse();
    }

    // A motionEye at pi-garage:8765: 0.44's sign-in (a session cookie) or an older one (no sign-in;
    // each request signed), the camera list and a picture.
    private sealed class FakeMotionEye(bool older) : HttpMessageHandler
    {
        public string Password { get; init; } = "secret";

        public string Page { get; init; } = "<html><title>motionEye</title></html>";

        public bool Down { get; init; }

        public HttpRequestMessage? ListRequest { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (Down)
            {
                throw new HttpRequestException("No route to host");
            }

            var path = request.RequestUri!.AbsolutePath;
            if (path == "/login" && request.Method == HttpMethod.Post)
            {
                if (older)
                {
                    return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(string.Empty) };
                }

                var form = await request.Content!.ReadAsStringAsync(cancellationToken);
                if (!form.Contains("password=" + Password, StringComparison.Ordinal))
                {
                    return new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("{\"error\": \"invalid credentials\"}") };
                }

                var ok = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"user\": \"admin\"}") };
                ok.Headers.Add("Set-Cookie", "user=session-1; HttpOnly; Path=/");
                return ok;
            }

            if (path == "/")
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Page) };
            }

            var signedIn = older
                ? request.RequestUri.Query.Contains("_signature=" + MotionEyeHubProvider.Sign("GET", request.RequestUri.PathAndQuery, body: null, Password), StringComparison.Ordinal)
                : request.Headers.TryGetValues("Cookie", out var cookies) && cookies.Contains("user=session-1", StringComparer.Ordinal);
            if (!signedIn)
            {
                return new HttpResponseMessage(HttpStatusCode.Forbidden) { Content = new StringContent("{\"error\": \"unauthorized\"}") };
            }

            if (path == "/config/list")
            {
                ListRequest = request;
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Cameras) };
            }

            if (path == "/picture/1/current")
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([0xFF, 0xD8, 0xFF]) { Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg") } } };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }
}