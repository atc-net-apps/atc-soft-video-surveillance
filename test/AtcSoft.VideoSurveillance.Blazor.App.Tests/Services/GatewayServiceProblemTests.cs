namespace AtcSoft.VideoSurveillance.Blazor.App.Services;

/// <summary>
/// A change the server refuses comes back with the server's own words,
/// which the pages show.
/// </summary>
public sealed class GatewayServiceProblemTests
{
    private const string Validation =
        """{"type":"https://tools.ietf.org/html/rfc9110#section-15.5.1","title":"One or more validation errors occurred.","status":400,"errors":{"port":["The field port must be between 1 and 65535."]}}""";

    [Fact]
    public async Task A_Camera_The_Server_Refuses_Comes_Back_With_Each_Field_Message()
    {
        using var server = GatewayTestServer.Answering(System.Net.HttpStatusCode.BadRequest, Validation);

        var result = await server.Gateway.CreateCameraAsync(new CreateCameraRequest(DisplayName: "Gate"), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Value.Should().BeNull();
        result.Problem.Should().Be("The field port must be between 1 and 65535.");
    }

    [Fact]
    public async Task A_Recording_That_Cannot_Start_Comes_Back_With_The_Reason()
    {
        using var server = GatewayTestServer.Answering(System.Net.HttpStatusCode.Conflict, """{"title":"Conflict","status":409,"detail":"The camera is unplugged."}""");

        var result = await server.Gateway.StartRecordingAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.Problem.Should().Be("The camera is unplugged.");
    }

    [Fact]
    public async Task A_Camera_That_Cannot_Be_Deleted_Comes_Back_With_The_Reason()
    {
        using var server = GatewayTestServer.Answering(System.Net.HttpStatusCode.NotFound, """{"title":"Not Found","status":404,"detail":"Camera 42 not found."}""");

        var result = await server.Gateway.DeleteCameraAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Problem.Should().Be("Camera 42 not found.");
    }

    [Fact]
    public async Task A_Server_That_Cannot_Be_Reached_Comes_Back_As_Unavailable_Without_An_Exception()
    {
        using var server = new GatewayTestServer(_ => throw new HttpRequestException("TypeError: Failed to fetch"));

        var result = await server.Gateway.UpdateSettingsAsync(new UpdateSettingsRequest(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeFalse();
        result.Problem.Should().Be(Translations.ServerCouldNotBeReached);
    }

    [Fact]
    public async Task A_Snapshot_From_A_Server_That_Cannot_Be_Reached_Is_No_Picture_Without_An_Exception()
    {
        using var server = new GatewayTestServer(_ => throw new HttpRequestException("TypeError: Failed to fetch"));

        var picture = await server.Gateway.CaptureSnapshotAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        picture.Should().BeNull();
    }

    [Fact]
    public async Task A_Change_The_Server_Accepts_Comes_Back_With_Its_Value()
    {
        using var server = GatewayTestServer.Answering(
            System.Net.HttpStatusCode.OK,
            """{"cameraId":"6f0c3b8e-0000-0000-0000-000000000001","state":"recording"}""");

        var result = await server.Gateway.StartRecordingAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.IsSuccess.Should().BeTrue();
        result.Value!.State.Should().Be(RecordingStatusState.Recording);
    }
}