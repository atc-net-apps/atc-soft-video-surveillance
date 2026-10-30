namespace AtcSoft.VideoSurveillance.Api.Host;

[Collection(ApiHostTestGroup.Name)]
public sealed class ApiHostStorageTests(ApiHostFixture host)
{
    [Fact]
    public async Task The_Server_Keeps_Its_Files_In_The_Configured_Data_Folder()
    {
        using var client = host.CreateClient();

        var settings = await client.GetFromJsonAsync<JsonElement>("/settings", TestContext.Current.CancellationToken);

        File.Exists(Path.Combine(host.DataPath, "settings.json")).Should().BeTrue();
        settings.GetProperty("recordingPath").GetString().Should().Be(host.DefaultRecordingPath);
        settings.GetProperty("snapshotPath").GetString().Should().Be(Path.Combine(host.DataPath, "snapshots"));
        settings.GetProperty("logPath").GetString().Should().Be(Path.Combine(host.DataPath, "logs"));
    }

    [Fact]
    public async Task A_Camera_Added_Over_Http_Is_Stored_In_The_Data_Folder_And_Listed()
    {
        using var client = host.CreateClient();
        var name = "Host test " + Guid.NewGuid().ToString("N")[..8];

        var created = await client.PostAsJsonAsync(
            "/cameras",
            new { displayName = name, ipAddress = "192.0.2.10", port = 554, protocol = "rtsp", path = "stream1" },
            TestContext.Current.CancellationToken);

        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>(TestContext.Current.CancellationToken)).GetProperty("id").GetGuid();

        var cameras = await client.GetFromJsonAsync<JsonElement>("/cameras", TestContext.Current.CancellationToken);
        cameras.EnumerateArray().Should().Contain(c => c.GetProperty("id").GetGuid() == id && c.GetProperty("displayName").GetString() == name);
        (await File.ReadAllTextAsync(Path.Combine(host.DataPath, "cameras.json"), TestContext.Current.CancellationToken)).Should().Contain(name);

        var deleted = await client.DeleteAsync(new Uri($"/cameras/{id}", UriKind.Relative), TestContext.Current.CancellationToken);
        deleted.IsSuccessStatusCode.Should().BeTrue();
        var afterDelete = await client.GetAsync(new Uri($"/cameras/{id}", UriKind.Relative), TestContext.Current.CancellationToken);
        afterDelete.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}