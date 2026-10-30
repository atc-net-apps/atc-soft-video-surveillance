namespace AtcSoft.VideoSurveillance.Api.Host;

/// <summary>
/// Every error the server answers is an RFC 9457 ProblemDetails, as the
/// specification says: from a handler, and from the framework itself (an
/// unknown address, a body that is no JSON).
/// </summary>
[Collection(ApiHostTestGroup.Name)]
public sealed class ApiHostErrorTests(ApiHostFixture host)
{
    [Fact]
    public async Task A_Camera_That_Is_Not_There_Is_A_Problem_With_The_Reason()
    {
        using var client = host.CreateClient();

        using var response = await client.GetAsync(new Uri($"/cameras/{Guid.NewGuid()}", UriKind.Relative), TestContext.Current.CancellationToken);

        var problem = await ReadProblemAsync(response, HttpStatusCode.NotFound);
        problem.GetProperty("detail").GetString().Should().NotBeNullOrWhiteSpace();
        problem.GetProperty("title").GetString().Should().Be("Not Found");
        problem.GetProperty("type").GetString().Should().Be("https://tools.ietf.org/html/rfc9110#section-15.5.5");
    }

    [Fact]
    public async Task A_Receiver_Without_An_Http_Address_Is_A_Bad_Request_Problem_With_The_Reason()
    {
        using var client = host.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/webhooks",
            new { url = "ftp://192.0.2.1/hook" },
            TestContext.Current.CancellationToken);

        var problem = await ReadProblemAsync(response, HttpStatusCode.BadRequest);
        problem.GetProperty("detail").GetString().Should().Contain("http");
    }

    [Fact]
    public async Task An_Address_The_Server_Does_Not_Have_Is_A_Problem()
    {
        using var client = host.CreateClient();

        using var response = await client.GetAsync(new Uri("/no-such-thing", UriKind.Relative), TestContext.Current.CancellationToken);

        await ReadProblemAsync(response, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_Body_That_Is_No_Json_Is_A_Problem()
    {
        using var client = host.CreateClient();
        using var body = new StringContent("{ not json", System.Text.Encoding.UTF8, "application/json");

        using var response = await client.PostAsync(new Uri("/cameras", UriKind.Relative), body, TestContext.Current.CancellationToken);

        await ReadProblemAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task An_Unexpected_Failure_Outside_Development_Is_A_Problem_Without_The_Exception()
    {
        // Makes sure the test data folder is in place before a second server starts on it.
        using var fixtureClient = host.CreateClient();

        var storage = Substitute.For<ICameraStorageService>();
        storage.GetAllCameras().Returns([]);
        storage.GetAllLayouts().Returns([]);
        storage.GetCameraById(Arg.Any<Guid>()).Returns(_ => throw new InvalidOperationException("secret inner detail"));
        await using var factory = new WebApplicationFactory<Program>();
        await using var production = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.ConfigureTestServices(services => services.AddSingleton(storage));
        });
        using var client = production.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", host.GetAdminAccessToken());

        using var response = await client.GetAsync(new Uri($"/cameras/{Guid.NewGuid()}", UriKind.Relative), TestContext.Current.CancellationToken);

        await ReadProblemAsync(response, HttpStatusCode.InternalServerError);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).Should().NotContain("secret inner detail");
    }

    [Fact]
    public async Task A_Camera_Is_Written_With_The_Enum_Values_Of_The_Specification()
    {
        using var client = host.CreateClient();
        using var created = await client.PostAsJsonAsync(
            "/cameras",
            new { displayName = "Enum values " + Guid.NewGuid().ToString("N")[..8], ipAddress = "192.0.2.11", port = 554, protocol = "rtsp", rotation = "rotate90" },
            TestContext.Current.CancellationToken);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("id").GetGuid();

        try
        {
            var camera = await client.GetFromJsonAsync<JsonElement>($"/cameras/{id}", TestContext.Current.CancellationToken);

            camera.GetProperty("source").GetString().Should().Be("network");
            camera.GetProperty("protocol").GetString().Should().Be("rtsp");
            camera.GetProperty("rotation").GetString().Should().Be("rotate90");
            camera.GetProperty("overlayPosition").GetString().Should().Be("topLeft");
        }
        finally
        {
            using var deleted = await client.DeleteAsync(new Uri($"/cameras/{id}", UriKind.Relative), TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task A_Camera_From_A_Client_Built_On_Generator_V2_Is_Stored_With_The_Enum_Values_Of_The_Specification()
    {
        // The server reads a listed value in any casing, and every value of ours is its
        // member name in camelCase, so what a 2.x client writes (Rtsp) is read as rtsp.
        using var client = host.CreateClient();
        using var created = await client.PostAsJsonAsync(
            "/cameras",
            new { displayName = "Member names " + Guid.NewGuid().ToString("N")[..8], ipAddress = "192.0.2.12", port = 554, protocol = "Rtsp", rotation = "Rotate90", overlayPosition = "TopLeft" },
            TestContext.Current.CancellationToken);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("id").GetGuid();

        try
        {
            var camera = await client.GetFromJsonAsync<JsonElement>($"/cameras/{id}", TestContext.Current.CancellationToken);

            camera.GetProperty("protocol").GetString().Should().Be("rtsp");
            camera.GetProperty("rotation").GetString().Should().Be("rotate90");
            camera.GetProperty("overlayPosition").GetString().Should().Be("topLeft");
        }
        finally
        {
            using var deleted = await client.DeleteAsync(new Uri($"/cameras/{id}", UriKind.Relative), TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task A_Camera_With_An_Enum_Value_The_Specification_Does_Not_List_Is_A_Bad_Request()
    {
        using var client = host.CreateClient();

        using var created = await client.PostAsJsonAsync(
            "/cameras",
            new { displayName = "Unknown value " + Guid.NewGuid().ToString("N")[..8], ipAddress = "192.0.2.13", port = 554, protocol = "ftp" },
            TestContext.Current.CancellationToken);

        await ReadProblemAsync(created, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task The_Settings_Are_Written_With_The_Enum_Values_Of_The_Specification()
    {
        using var client = host.CreateClient();

        var settings = await client.GetFromJsonAsync<JsonElement>("/settings", TestContext.Current.CancellationToken);

        settings.GetProperty("overlayPosition").GetString().Should().Be("topLeft");
        settings.GetProperty("defaultProtocol").GetString().Should().Be("rtsp");
    }

    private static async Task<JsonElement> ReadProblemAsync(
        HttpResponseMessage response,
        HttpStatusCode status)
    {
        response.StatusCode.Should().Be(status);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        problem.GetProperty("status").GetInt32().Should().Be((int)status);
        return problem;
    }
}