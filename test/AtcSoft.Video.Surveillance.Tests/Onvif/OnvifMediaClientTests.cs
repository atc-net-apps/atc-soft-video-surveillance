namespace AtcSoft.Video.Surveillance.Helpers;

/// <summary>
/// The client against a fake camera that answers each SOAP action as a camera does.
/// </summary>
public sealed class OnvifMediaClientTests
{
    [Fact]
    public async Task The_Profiles_Are_Read_And_The_Streams_Chosen()
    {
        using var camera = new FakeCamera();
        using var http = new HttpClient(camera);
        var sut = new OnvifMediaClient(http);

        var result = await sut.ReadProfilesAsync("192.0.2.10", port: 80, "admin", "secret", TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(OnvifImportOutcome.Read);
        result.Profiles.Should().HaveCount(3);
        result.Choice!.Main.StreamUri!.PathAndQuery.Should().Be("/Streaming/Channels/Profile_1");
        result.Choice.Sub!.StreamUri!.PathAndQuery.Should().Be("/Streaming/Channels/Profile_2");
        result.Profiles.Should().OnlyContain(p => string.IsNullOrEmpty(p.StreamUri!.UserInfo), "no user info is kept");
    }

    [Fact]
    public async Task The_Sign_In_Token_Goes_Only_To_The_Requests_That_Need_It()
    {
        using var camera = new FakeCamera();
        using var http = new HttpClient(camera);
        var sut = new OnvifMediaClient(http);

        await sut.ReadProfilesAsync("192.0.2.10", port: 80, "admin", "secret", TestContext.Current.CancellationToken);

        camera.Requests.Single(r => r.Action.EndsWith("GetSystemDateAndTime", StringComparison.Ordinal)).Body.Should().NotContain("UsernameToken");
        camera.Requests.Where(r => !r.Action.EndsWith("GetSystemDateAndTime", StringComparison.Ordinal))
            .Should().OnlyContain(r => r.Body.Contains("PasswordDigest", StringComparison.Ordinal) && !r.Body.Contains("secret", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_Media_Service_Is_Asked_At_The_Host_That_Was_Typed()
    {
        // The camera names 10.0.0.99, an address of its own the server may not reach.
        using var camera = new FakeCamera();
        using var http = new HttpClient(camera);
        var sut = new OnvifMediaClient(http);

        await sut.ReadProfilesAsync("192.0.2.10", port: 80, "admin", "secret", TestContext.Current.CancellationToken);

        camera.Requests.Should().OnlyContain(r => r.Uri.Host == "192.0.2.10");
        camera.Requests.Should().Contain(r => r.Uri.AbsolutePath == "/onvif/Media");
    }

    [Fact]
    public async Task Refused_Credentials_Are_Told_Apart()
    {
        using var camera = new FakeCamera { RefuseSignIn = true };
        using var http = new HttpClient(camera);
        var sut = new OnvifMediaClient(http);

        var result = await sut.ReadProfilesAsync("192.0.2.10", port: 80, "admin", "wrong", TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(OnvifImportOutcome.Unauthorized);
    }

    [Fact]
    public async Task A_Device_That_Is_No_Onvif_Camera_Is_Told_Apart()
    {
        using var camera = new FakeCamera { IsOnvif = false };
        using var http = new HttpClient(camera);
        var sut = new OnvifMediaClient(http);

        var result = await sut.ReadProfilesAsync("192.0.2.10", port: 80, "admin", "secret", TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(OnvifImportOutcome.NotOnvif);
    }

    private sealed class FakeCamera : HttpMessageHandler
    {
        public bool IsOnvif { get; init; } = true;

        public bool RefuseSignIn { get; init; }

        public List<(Uri Uri, string Action, string Body)> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            var action = request.Content.Headers.ContentType!.Parameters.Single(p => p.Name == "action").Value!.Trim('"');
            lock (Requests)
            {
                Requests.Add((request.RequestUri!, action, body));
            }

            if (!IsOnvif)
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound) { Content = new StringContent("<html>Not found</html>") };
            }

            var operation = action[(action.LastIndexOf('/', StringComparison.Ordinal) + 1)..];
            if (operation != "GetSystemDateAndTime" && RefuseSignIn)
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.BadRequest) { Content = new StringContent(OnvifResponses.NotAuthorizedFault) };
            }

            var answer = operation switch
            {
                "GetSystemDateAndTime" => OnvifResponses.SystemDateAndTime,
                "GetCapabilities" => OnvifResponses.Capabilities,
                "GetProfiles" => OnvifResponses.HikvisionProfiles,
                "GetStreamUri" => OnvifResponses.StreamUri(
                    $"rtsp://admin:secret@10.0.0.99:554/Streaming/Channels/{System.Xml.Linq.XDocument.Parse(body).Descendants().Single(e => e.Name.LocalName == "ProfileToken").Value}"),
                _ => throw new InvalidOperationException(operation),
            };

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(answer) };
        }
    }
}