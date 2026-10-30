namespace AtcSoft.VideoSurveillance.Api.Host;

/// <summary>
/// A viewer watches a live stream on a lease it renews; what the server
/// answers when the camera or the lease is not there.
/// </summary>
[Collection(ApiHostTestGroup.Name)]
public sealed class ApiHostStreamLeaseTests(ApiHostFixture host)
{
    [Fact]
    public async Task A_Stream_Of_A_Camera_The_Server_Does_Not_Know_Is_Not_Found()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = host.CreateClient();
        var cameraId = Guid.NewGuid();

        var started = await client.PostAsync(new Uri($"/cameras/{cameraId}/stream", UriKind.Relative), content: null, cancellationToken);

        started.StatusCode.Should().Be(HttpStatusCode.NotFound);
        var problem = await started.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        problem.GetProperty("detail").GetString().Should().Be($"Camera {cameraId} not found.");
    }

    [Fact]
    public async Task A_Lease_The_Server_Does_Not_Have_Cannot_Be_Renewed_And_Giving_It_Back_Is_No_Error()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var client = host.CreateClient();
        var leaseUri = new Uri($"/cameras/{Guid.NewGuid()}/stream/{Guid.NewGuid()}", UriKind.Relative);

        var renewed = await client.PutAsync(leaseUri, content: null, cancellationToken);
        var released = await client.DeleteAsync(leaseUri, cancellationToken);

        // The viewer starts a new stream on a 404 (the server may have restarted);
        // a lease that is already gone is given back all the same.
        renewed.StatusCode.Should().Be(HttpStatusCode.NotFound);
        renewed.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        released.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}