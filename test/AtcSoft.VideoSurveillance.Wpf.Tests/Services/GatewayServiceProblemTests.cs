namespace AtcSoft.VideoSurveillance.Wpf.Services;

/// <summary>
/// A change the server refuses is an error with the server's own words,
/// which the view models show.
/// </summary>
public sealed class GatewayServiceProblemTests
{
    private const string Validation =
        """{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"One or more validation errors occurred.","status":400,"errors":{"port":["The field port must be between 1 and 65535."],"displayName":["The field displayName must be a string or array type with a minimum length of '1'."]}}""";

    [Fact]
    public async Task A_Camera_The_Server_Refuses_Is_An_Error_With_Each_Field_Message()
    {
        using var body = new StringContent(Validation);
        using var server = new GatewayTestServer(HttpStatusCode.BadRequest, body, "application/problem+json");

        var create = () => server.Gateway.CreateCameraAsync(new global::VideoSurveillance.Generated.Cameras.Models.CreateCameraRequest(DisplayName: string.Empty), TestContext.Current.CancellationToken);

        var error = (await create.Should().ThrowAsync<HttpRequestException>()).Which;
        error.Message.Should().Contain("The field port must be between 1 and 65535.");
        error.Message.Should().Contain("The field displayName must be a string or array type with a minimum length of '1'.");
        error.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_Camera_That_Is_Gone_Is_An_Error_With_The_Reason()
    {
        using var body = new StringContent("""{"title":"Not Found","status":404,"detail":"Camera 42 not found."}""");
        using var server = new GatewayTestServer(HttpStatusCode.NotFound, body, "application/problem+json");

        var update = () => server.Gateway.UpdateCameraAsync(Guid.NewGuid(), new global::VideoSurveillance.Generated.Cameras.Models.UpdateCameraRequest(), TestContext.Current.CancellationToken);

        (await update.Should().ThrowAsync<HttpRequestException>()).Which.Message.Should().Be("Camera 42 not found.");
    }

    [Fact]
    public async Task A_Recording_That_Cannot_Start_Is_An_Error_With_The_Reason()
    {
        using var body = new StringContent("""{"title":"Conflict","status":409,"detail":"The camera is unplugged."}""");
        using var server = new GatewayTestServer(HttpStatusCode.Conflict, body, "application/problem+json");

        var start = () => server.Gateway.StartRecordingAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        (await start.Should().ThrowAsync<HttpRequestException>()).Which.Message.Should().Be("The camera is unplugged.");
    }

    [Fact]
    public async Task A_Failure_Without_A_Problem_Is_An_Error_With_The_Status()
    {
        using var body = new StringContent(string.Empty);
        using var server = new GatewayTestServer(HttpStatusCode.ServiceUnavailable, body, "text/plain");

        var delete = () => server.Gateway.DeleteCameraAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        (await delete.Should().ThrowAsync<HttpRequestException>()).Which.Message.Should().Contain("503");
    }
}