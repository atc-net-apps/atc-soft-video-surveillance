namespace AtcSoft.VideoSurveillance.Api.Host;

/// <summary>
/// The limits in the specification are checked before a handler runs: a
/// value outside them is a 400 validation problem that names the field.
/// </summary>
[Collection(ApiHostTestGroup.Name)]
public sealed class ApiHostValidationTests(ApiHostFixture host)
{
    [Fact]
    public async Task A_Camera_Without_A_Name_And_With_A_Port_Out_Of_Range_Is_Refused_By_Field()
    {
        using var client = host.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/cameras",
            new { displayName = string.Empty, ipAddress = "192.0.2.20", port = 70000 },
            TestContext.Current.CancellationToken);

        var errors = await ReadErrorsAsync(response);
        errors.TryGetProperty("displayName", out _).Should().BeTrue();
        errors.TryGetProperty("port", out _).Should().BeTrue();
    }

    [Fact]
    public async Task A_Settings_Update_With_A_Timeout_Of_Zero_Is_Refused_And_Changes_Nothing()
    {
        using var client = host.CreateClient();
        var before = await client.GetFromJsonAsync<JsonElement>("/settings", TestContext.Current.CancellationToken);

        using var response = await client.PutAsJsonAsync(
            "/settings",
            new { connectionTimeoutSeconds = 0, showOverlayTime = !before.GetProperty("showOverlayTime").GetBoolean() },
            TestContext.Current.CancellationToken);

        var errors = await ReadErrorsAsync(response);
        errors.TryGetProperty("connectionTimeoutSeconds", out _).Should().BeTrue();
        var after = await client.GetFromJsonAsync<JsonElement>("/settings", TestContext.Current.CancellationToken);
        after.GetRawText().Should().Be(before.GetRawText());
    }

    [Fact]
    public async Task A_Layout_Without_A_Name_And_Without_Rows_Is_Refused_By_Field()
    {
        using var client = host.CreateClient();

        using var response = await client.PostAsJsonAsync(
            "/layouts",
            new { name = string.Empty, rows = 0, columns = 2 },
            TestContext.Current.CancellationToken);

        var errors = await ReadErrorsAsync(response);
        errors.TryGetProperty("name", out _).Should().BeTrue();
        errors.TryGetProperty("rows", out _).Should().BeTrue();
    }

    [Theory]
    [InlineData("two corners")]
    [InlineData("a corner outside the picture")]
    [InlineData("seventeen zones")]
    [InlineData("a sensitivity above 100")]
    [InlineData("a smallest object below nothing")]
    public async Task Zones_Outside_Their_Limits_Are_Refused(string what)
    {
        using var client = host.CreateClient();
        object[] zones = what switch
        {
            "two corners" => [Zone(0.1, corners: 2)],
            "a corner outside the picture" => [Zone(1.5)],
            "a sensitivity above 100" => [WatchZone(sensitivity: 101, smallestObjectPercent: 0.1)],
            "a smallest object below nothing" => [WatchZone(sensitivity: 50, smallestObjectPercent: -1)],
            _ => [.. Enumerable.Repeat(Zone(0.1), 17)],
        };

        using var response = await client.PostAsJsonAsync(
            "/cameras",
            new { displayName = "Zones", ipAddress = "192.0.2.21", motionZones = zones },
            TestContext.Current.CancellationToken);

        var errors = await ReadErrorsAsync(response);
        errors.EnumerateObject().Should().Contain(e => e.Name.StartsWith("motionZones", StringComparison.OrdinalIgnoreCase), what);
    }

    [Fact]
    public async Task Motion_Zones_Inside_Their_Limits_Are_Kept()
    {
        using var client = host.CreateClient();

        using var created = await client.PostAsJsonAsync(
            "/cameras",
            new { displayName = "Zones", ipAddress = "192.0.2.22", motionZones = new[] { Zone(0.1), WatchZone(sensitivity: 80, smallestObjectPercent: 0.1) } },
            TestContext.Current.CancellationToken);

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var camera = await created.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        camera.GetProperty("motionZones")[0].GetProperty("points").GetArrayLength().Should().Be(3);
        var watch = camera.GetProperty("motionZones")[1];
        watch.GetProperty("kind").GetString().Should().Be("watch");
        watch.GetProperty("sensitivity").GetInt32().Should().Be(80);
        watch.GetProperty("smallestObjectPercent").GetDouble().Should().Be(0.1);
        using var deleted = await client.DeleteAsync(new Uri($"/cameras/{camera.GetProperty("id").GetGuid()}", UriKind.Relative), TestContext.Current.CancellationToken);
    }

    private static object Zone(
        double x,
        int corners = 3)
        => new
        {
            name = "Tree",
            kind = "ignore",
            points = new object[] { new { x, y = 0.1 }, new { x = 0.5, y = 0.1 }, new { x = 0.5, y = 0.5 } }.Take(corners).ToArray(),
        };

    private static object WatchZone(
        int sensitivity,
        double smallestObjectPercent)
        => new
        {
            name = "Gate",
            kind = "watch",
            points = new object[] { new { x = 0.1, y = 0.1 }, new { x = 0.5, y = 0.1 }, new { x = 0.5, y = 0.5 } },
            sensitivity,
            smallestObjectPercent,
        };

    private static async Task<JsonElement> ReadErrorsAsync(
        HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/problem+json");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return problem.GetProperty("errors");
    }
}