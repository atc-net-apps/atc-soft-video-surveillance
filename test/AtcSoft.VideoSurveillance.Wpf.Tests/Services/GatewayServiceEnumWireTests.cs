namespace AtcSoft.VideoSurveillance.Wpf.Services;

/// <summary>
/// The client's enums are extensible: a listed value is read in any casing,
/// and a value the client does not know yet is kept instead of failing the
/// whole response.
/// </summary>
public sealed class GatewayServiceEnumWireTests
{
    [Fact]
    public async Task A_Camera_Is_Read_By_The_Values_Of_The_Specification()
    {
        using var body = CameraBody("rtsp", "rotate90", "topLeft", "connected");
        using var server = new GatewayTestServer(HttpStatusCode.OK, body, "application/json");

        var camera = await server.Gateway.GetCameraByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        camera.Should().NotBeNull();
        camera!.Protocol.Should().Be(global::VideoSurveillance.Generated.Cameras.Models.CameraProtocol.Rtsp);
        camera.Rotation.Should().Be(global::VideoSurveillance.Generated.Cameras.Models.CameraRotation.Rotate90);
        camera.OverlayPosition.Should().Be(global::VideoSurveillance.Generated.Cameras.Models.CameraOverlayPosition.TopLeft);
        camera.ConnectionState.Should().Be(global::VideoSurveillance.Generated.Models.CameraConnectionState.Connected);
    }

    [Fact]
    public async Task A_Camera_From_A_Server_Built_On_Generator_V2_Is_Read_Too()
    {
        // A 2.x server wrote the C# member names: the listed values in another casing.
        using var body = CameraBody("Rtsp", "Rotate90", "TopLeft", "Connected");
        using var server = new GatewayTestServer(HttpStatusCode.OK, body, "application/json");

        var camera = await server.Gateway.GetCameraByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        camera!.Protocol.Should().Be(global::VideoSurveillance.Generated.Cameras.Models.CameraProtocol.Rtsp);
        camera.ConnectionState.Should().Be(global::VideoSurveillance.Generated.Models.CameraConnectionState.Connected);
    }

    [Fact]
    public async Task A_State_The_Client_Does_Not_Know_Yet_Is_Kept_And_The_Camera_Is_Read()
    {
        using var body = CameraBody("rtsp", "none", "topLeft", "rebooting");
        using var server = new GatewayTestServer(HttpStatusCode.OK, body, "application/json");

        var camera = await server.Gateway.GetCameraByIdAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        camera.Should().NotBeNull();
        camera!.DisplayName.Should().Be("Gate");
        camera.ConnectionState!.Value.IsKnown.Should().BeFalse();
        camera.ConnectionState.Value.Value.Should().Be("rebooting");
    }

    private static StringContent CameraBody(
        string protocol,
        string rotation,
        string overlayPosition,
        string connectionState)
        => new(
            $$"""{"id":"{{Guid.NewGuid()}}","displayName":"Gate","protocol":"{{protocol}}","rotation":"{{rotation}}","overlayPosition":"{{overlayPosition}}","connectionState":"{{connectionState}}"}""");
}