namespace AtcSoft.Video.Surveillance.Api.Handlers.Hubs;

using CoreCamera = AtcSoft.Video.Surveillance.Models.CameraConfiguration;

/// <summary>
/// A camera hub's cameras through the server: listed with a picture each and without the
/// streams' credentials, and added as a camera with them, read from the hub by the server.
/// </summary>
public sealed class HubHandlerTests
{
    private static readonly Uri Hub = new("http://pi-garage:8765/");

    private static readonly HubCamera Garage = new("1", "Garage", new Uri("http://pi-garage:8081/"), HubStreamKind.Mjpeg, "viewer", "pw", "1280x720", 5, IsUsb: true, HubCameraProblem.None);

    private static readonly HubCamera Off = new("4", "Local only", null, HubStreamKind.Mjpeg, null, null, null, null, IsUsb: true, HubCameraProblem.StreamingOff);

    [Fact]
    public async Task A_Hubs_Cameras_Are_Listed_With_A_Picture_Each()
    {
        var sut = new ProbeHubHandler(Provider());

        var result = await sut.ExecuteAsync(new ProbeHubParameters(new HubProbeRequest(Address: "pi-garage", UserName: "admin", Password: "secret")), TestContext.Current.CancellationToken);

        var listing = result.Result.Should().BeOfType<Ok<HubProbeResult>>().Subject.Value!;
        listing.Outcome.Should().Be(HubProbeResultOutcome.Listed);
        listing.Address.Should().Be("http://pi-garage:8765/");
        listing.Cameras.Select(c => (c.Name, c.Problem)).Should().Equal(("Garage", HubCameraInfoProblem.None), ("Local only", HubCameraInfoProblem.StreamingOff));
        listing.Cameras[0].Picture.Should().Equal(0xFF, 0xD8);
        listing.Cameras[0].IsUsb.Should().BeTrue();
    }

    [Fact]
    public async Task Something_That_Is_No_Address_Is_Refused()
    {
        var result = await new ProbeHubHandler(Provider()).ExecuteAsync(new ProbeHubParameters(new HubProbeRequest(Address: "ftp://x")), TestContext.Current.CancellationToken);

        result.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task A_Hub_Camera_Is_Added_With_Its_Stream_Credentials_From_The_Hub()
    {
        var storage = Substitute.For<ICameraStorageService>();
        CoreCamera? added = null;
        storage.When(s => s.AddOrUpdateCamera(Arg.Any<CoreCamera>())).Do(c => added = c.Arg<CoreCamera>());
        var sut = new AddHubCameraHandler(Provider(), storage);

        var result = await sut.ExecuteAsync(new AddHubCameraParameters(new AddHubCameraRequest(Address: "pi-garage", CameraId: "1", UserName: "admin", Password: "secret")), TestContext.Current.CancellationToken);

        result.StatusCode.Should().Be(StatusCodes.Status201Created);
        added!.BuildUri().Should().Be(new Uri("http://viewer:pw@pi-garage:8081/"));
        added.Display.DisplayName.Should().Be("Garage");
        added.Connection.Hub!.CameraId.Should().Be("1");
        storage.Received(1).Save();
    }

    [Theory]
    [InlineData("4", "Video Streaming")]
    [InlineData("9", "no camera")]
    public async Task A_Camera_That_Cannot_Be_Added_Says_Why(
        string cameraId,
        string reason)
    {
        var storage = Substitute.For<ICameraStorageService>();
        var sut = new AddHubCameraHandler(Provider(), storage);

        var result = await sut.ExecuteAsync(new AddHubCameraParameters(new AddHubCameraRequest(Address: "pi-garage", CameraId: cameraId)), TestContext.Current.CancellationToken);

        result.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        result.Problem!.Detail.Should().Contain(reason);
        storage.DidNotReceive().AddOrUpdateCamera(Arg.Any<CoreCamera>());
    }

    private static ICameraHubProvider Provider()
    {
        var provider = Substitute.For<ICameraHubProvider>();
        provider.ListAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(new CameraHubListing(CameraHubOutcome.Listed, CameraHubKinds.MotionEye, Hub, [Garage, Off]));
        provider.GetSnapshotAsync(Hub, Arg.Any<string?>(), Arg.Any<string?>(), "1", Arg.Any<CancellationToken>())
            .Returns(new byte[] { 0xFF, 0xD8 });
        return provider;
    }
}