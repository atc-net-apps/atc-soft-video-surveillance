namespace AtcSoft.VideoSurveillance.Api.Host;

/// <summary>
/// A settings update over the wire changes only what it sends.
/// </summary>
[Collection(ApiHostTestGroup.Name)]
public sealed class ApiHostSettingsTests(ApiHostFixture host)
{
    [Fact]
    public async Task An_Update_Without_Settings_Changes_Nothing()
    {
        using var client = host.CreateClient();
        var before = await client.GetFromJsonAsync<JsonElement>("/settings", TestContext.Current.CancellationToken);

        using var updated = await client.PutAsJsonAsync("/settings", new { }, TestContext.Current.CancellationToken);

        updated.StatusCode.Should().Be(HttpStatusCode.OK);
        var after = await client.GetFromJsonAsync<JsonElement>("/settings", TestContext.Current.CancellationToken);
        after.GetRawText().Should().Be(before.GetRawText());
    }

    [Fact]
    public async Task An_Update_Changes_Only_The_Settings_It_Sends()
    {
        using var client = host.CreateClient();
        var before = await client.GetFromJsonAsync<JsonElement>("/settings", TestContext.Current.CancellationToken);
        var showTime = !before.GetProperty("showOverlayTime").GetBoolean();

        try
        {
            using var updated = await client.PutAsJsonAsync("/settings", new { showOverlayTime = showTime }, TestContext.Current.CancellationToken);

            updated.StatusCode.Should().Be(HttpStatusCode.OK);
            var after = await client.GetFromJsonAsync<JsonObject>("/settings", TestContext.Current.CancellationToken);
            after!["showOverlayTime"]!.GetValue<bool>().Should().Be(showTime);

            var expected = JsonNode.Parse(before.GetRawText())!.AsObject();
            expected["showOverlayTime"] = showTime;
            after.ToJsonString().Should().Be(expected.ToJsonString());
        }
        finally
        {
            using var restored = await client.PutAsJsonAsync("/settings", new { showOverlayTime = !showTime }, TestContext.Current.CancellationToken);
        }
    }
}