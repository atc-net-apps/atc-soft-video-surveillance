namespace AtcSoft.VideoSurveillance.Api.Host;

/// <summary>
/// The running server answers only a signed-in caller or an API key, each as
/// its role allows; the video files only with a lease or the media token.
/// </summary>
[Collection(ApiHostTestGroup.Name)]
public sealed class ApiHostSecurityTests(ApiHostFixture host)
{
    [Fact]
    public async Task Without_A_Token_The_Server_Says_401_As_A_Problem()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var anonymous = host.CreateAnonymousClient();

        var cameras = await anonymous.GetAsync(new Uri("/cameras", UriKind.Relative), cancellationToken);
        var events = await anonymous.GetAsync(new Uri("/events", UriKind.Relative), cancellationToken);

        cameras.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        cameras.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        events.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.GetAsync(new Uri("/health", UriKind.Relative), cancellationToken)).StatusCode.Should().NotBe(HttpStatusCode.Unauthorized, "a monitor asks without one");
    }

    [Fact]
    public async Task A_Viewer_Reads_But_Does_Not_Change()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var viewer = await CreateUserClientAsync("viewer");

        (await viewer.GetAsync(new Uri("/cameras", UriKind.Relative), cancellationToken)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await viewer.GetAsync(new Uri("/settings", UriKind.Relative), cancellationToken)).StatusCode.Should().Be(HttpStatusCode.OK);

        var created = await viewer.PostAsJsonAsync("/cameras", new { displayName = "Not allowed", ipAddress = "192.0.2.1" }, cancellationToken);
        var settings = await viewer.PutAsJsonAsync("/settings", new { showOverlayTitle = false }, cancellationToken);

        created.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        created.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        settings.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task An_Operator_Changes_Cameras_But_Not_The_Settings()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var operatorClient = await CreateUserClientAsync("operator");

        var created = await operatorClient.PostAsJsonAsync("/cameras", new { displayName = "By the operator " + Guid.NewGuid().ToString("N")[..6], ipAddress = "192.0.2.2" }, cancellationToken);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var cameraId = (await created.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetGuid();

        (await operatorClient.PutAsJsonAsync("/settings", new { showOverlayTitle = false }, cancellationToken)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await operatorClient.DeleteAsync(new Uri($"/cameras/{cameraId}", UriKind.Relative), cancellationToken)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task An_Api_Key_Acts_With_Its_Role()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var admin = host.CreateClient();
        var made = await admin.PostAsJsonAsync("/api-keys", new { name = "Integration " + Guid.NewGuid().ToString("N")[..6], role = "viewer" }, cancellationToken);
        var key = (await made.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("key").GetString()!;
        using var withKey = host.CreateAnonymousClient();
        withKey.DefaultRequestHeaders.Add("X-API-Key", key);
        using var wrongKey = host.CreateAnonymousClient();
        wrongKey.DefaultRequestHeaders.Add("X-API-Key", key + "x");

        (await withKey.GetAsync(new Uri("/cameras", UriKind.Relative), cancellationToken)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await withKey.PostAsJsonAsync("/cameras", new { displayName = "Not allowed", ipAddress = "192.0.2.3" }, cancellationToken)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await wrongKey.GetAsync(new Uri("/cameras", UriKind.Relative), cancellationToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task An_Operators_Api_Key_Makes_A_Change_And_The_Audit_Log_Names_The_Key()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var admin = host.CreateClient();
        var keyName = "Operator integration " + Guid.NewGuid().ToString("N")[..6];
        var made = await admin.PostAsJsonAsync("/api-keys", new { name = keyName, role = "operator" }, cancellationToken);
        var key = (await made.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("key").GetString()!;
        using var withKey = host.CreateAnonymousClient();
        withKey.DefaultRequestHeaders.Add("X-API-Key", key);

        var created = await withKey.PostAsJsonAsync("/cameras", new { displayName = "By a key " + Guid.NewGuid().ToString("N")[..6], ipAddress = "192.0.2.4" }, cancellationToken);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var cameraId = (await created.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("id").GetGuid();
        (await withKey.PutAsJsonAsync("/settings", new { showOverlayTitle = false }, cancellationToken)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var audit = await admin.GetFromJsonAsync<JsonElement>("/audit?take=200", cancellationToken);
        audit.EnumerateArray().Should().Contain(x =>
            x.GetProperty("userName").GetString() == keyName &&
            x.GetProperty("action").GetString() == "createCamera");

        (await admin.DeleteAsync(new Uri($"/cameras/{cameraId}", UriKind.Relative), cancellationToken)).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task A_Recording_Is_Served_With_A_Token_Or_The_Media_Token_And_Not_To_Anyone_Else()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await host.EnsureAdminAsync();
        var (_, session) = await host.SignInAsync(ApiHostFixture.AdminName, ApiHostFixture.AdminPassword);
        var mediaToken = session.GetProperty("mediaToken").GetString();
        var accessToken = session.GetProperty("accessToken").GetString();
        using var anonymous = host.CreateAnonymousClient();
        using var admin = host.CreateClient();
        const string missing = "/recordings-files/Nobody/2026-10-15/Nobody_20261015_120000.mp4";

        // The gate is passed (the file is not there), or it is not.
        (await anonymous.GetAsync(new Uri(missing, UriKind.Relative), cancellationToken)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await anonymous.GetAsync(new Uri($"{missing}?media={mediaToken}", UriKind.Relative), cancellationToken)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await admin.GetAsync(new Uri(missing, UriKind.Relative), cancellationToken)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await anonymous.GetAsync(new Uri($"{missing}?media={accessToken}", UriKind.Relative), cancellationToken)).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized, "an access token is not a media token");
    }

    [Fact]
    public async Task A_Streams_Files_Are_Only_There_Under_A_Live_Lease()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var admin = host.CreateClient();

        (await admin.GetAsync(new Uri($"/streams/{Guid.NewGuid():N}/stream.m3u8", UriKind.Relative), cancellationToken)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await admin.GetAsync(new Uri("/streams/../settings.json", UriKind.Relative), cancellationToken)).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private async Task<HttpClient> CreateUserClientAsync(string role)
    {
        using var admin = host.CreateClient();
        var name = role + "-" + Guid.NewGuid().ToString("N")[..8];
        (await admin.PostAsJsonAsync("/users", new { userName = name, password = "a password here", role }, TestContext.Current.CancellationToken))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        var (_, session) = await host.SignInAsync(name, "a password here");

        var client = host.CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", session.GetProperty("accessToken").GetString());
        return client;
    }
}