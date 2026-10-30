namespace AtcSoft.VideoSurveillance.Api.Host;

/// <summary>
/// A create answers 201 with the new resource and its address in Location.
/// </summary>
[Collection(ApiHostTestGroup.Name)]
public sealed class ApiHostCreatedTests(ApiHostFixture host)
{
    [Fact]
    public async Task A_New_Camera_Has_Its_Address_In_Location()
    {
        using var client = host.CreateClient();

        using var created = await client.PostAsJsonAsync(
            "/cameras",
            new { displayName = "Location " + Guid.NewGuid().ToString("N")[..8], ipAddress = "192.0.2.30", port = 554 },
            TestContext.Current.CancellationToken);

        var id = await ReadIdAsync(created);
        created.Headers.Location.Should().Be(new Uri($"/cameras/{id}", UriKind.Relative));
        using var deleted = await client.DeleteAsync(created.Headers.Location, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_New_Layout_Has_Its_Address_In_Location()
    {
        using var client = host.CreateClient();

        using var created = await client.PostAsJsonAsync(
            "/layouts",
            new { name = "Location " + Guid.NewGuid().ToString("N")[..8], rows = 1, columns = 1 },
            TestContext.Current.CancellationToken);

        var id = await ReadIdAsync(created);
        created.Headers.Location.Should().Be(new Uri($"/layouts/{id}", UriKind.Relative));
        using var deleted = await client.DeleteAsync(created.Headers.Location, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_New_Webhook_Receiver_Has_Its_Address_In_Location()
    {
        using var client = host.CreateClient();

        using var created = await client.PostAsJsonAsync(
            "/webhooks",
            new { url = "https://hooks.test/location-" + Guid.NewGuid().ToString("N")[..8] },
            TestContext.Current.CancellationToken);

        var id = await ReadIdAsync(created);
        created.Headers.Location.Should().Be(new Uri($"/webhooks/{id}", UriKind.Relative));
        using var deleted = await client.DeleteAsync(created.Headers.Location, TestContext.Current.CancellationToken);
    }

    private static async Task<Guid> ReadIdAsync(HttpResponseMessage created)
    {
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await created.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken);
        return body.GetProperty("id").GetGuid();
    }
}