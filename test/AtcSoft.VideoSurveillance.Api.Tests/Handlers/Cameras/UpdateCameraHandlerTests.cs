namespace AtcSoft.VideoSurveillance.Api.Handlers.Cameras;

public class UpdateCameraHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_CameraExists_UpdatesAndReturnsOk()
    {
        // Arrange
        var storage = Substitute.For<ICameraStorageService>();
        var cameraId = Guid.NewGuid();
        var camera = new CameraConfiguration { Id = cameraId };
        camera.Display.DisplayName = "Old Name";
        camera.Connection.IpAddress = "10.0.0.1";
        storage.GetCameraById(cameraId).Returns(camera);
        var handler = new UpdateCameraHandler(storage);

        var request = MinimalUpdate("New Name");

        // Act
        var result = await handler.ExecuteAsync(
            new UpdateCameraParameters(cameraId, request),
            CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<Ok<Camera>>().Subject;
        okResult.Value.Should().NotBeNull();
        okResult.Value!.DisplayName.Should().Be("New Name");
        storage.Received(1).AddOrUpdateCamera(camera);
        storage.Received(1).Save();
    }

    [Fact]
    public async Task ExecuteAsync_CameraNotFound_ReturnsNotFound()
    {
        // Arrange
        var storage = Substitute.For<ICameraStorageService>();
        var cameraId = Guid.NewGuid();
        storage.GetCameraById(cameraId).Returns((CameraConfiguration?)null);
        var handler = new UpdateCameraHandler(storage);

        var request = MinimalUpdate("Name");

        // Act
        var result = await handler.ExecuteAsync(
            new UpdateCameraParameters(cameraId, request),
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task ExecuteAsync_PartialUpdate_OnlyChangesProvidedFields()
    {
        // Arrange
        var storage = Substitute.For<ICameraStorageService>();
        var cameraId = Guid.NewGuid();
        var camera = new CameraConfiguration { Id = cameraId };
        camera.Display.DisplayName = "Keep This";
        camera.Connection.IpAddress = "192.168.1.1";
        camera.Connection.Port = 554;
        storage.GetCameraById(cameraId).Returns(camera);
        var handler = new UpdateCameraHandler(storage);

        var request = new UpdateCameraRequest(
            DisplayName: null!,
            Description: "New description",
            Source: null,
            ConnectionType: null,
            Rotation: null,
            IpAddress: null!,
            Port: 0,
            Protocol: null,
            Path: null!,
            Username: null!,
            Password: null!,
            UsbDeviceId: null!,
            UsbFriendlyName: null!,
            UsbWidth: 0,
            UsbHeight: 0,
            UsbFrameRate: 0,
            UsbPixelFormat: null!,
            UsbCaptureAudio: false,
            UsbAudioDeviceName: null!,
            OverlayPosition: null,
            StreamUseLowLatencyMode: true,
            StreamMaxLatencyMs: 500,
            StreamRtspTransport: null,
            StreamBufferDurationMs: 0);

        // Act
        var result = await handler.ExecuteAsync(
            new UpdateCameraParameters(cameraId, request),
            CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<Ok<Camera>>().Subject;
        okResult.Value!.DisplayName.Should().Be("Keep This");
        okResult.Value.Description.Should().Be("New description");
        okResult.Value.IpAddress.Should().Be("192.168.1.1");
    }

    [Fact]
    public async Task ExecuteAsync_SwitchingToUsbWithoutADevice_IsRefused_AndChangesNothing()
    {
        var storage = Substitute.For<ICameraStorageService>();
        var cameraId = Guid.NewGuid();
        var camera = new CameraConfiguration { Id = cameraId };
        camera.Display.DisplayName = "Kept";
        camera.Connection.IpAddress = "10.0.0.1";
        storage.GetCameraById(cameraId).Returns(camera);
        var handler = new UpdateCameraHandler(storage);

        var result = await handler.ExecuteAsync(
            new UpdateCameraParameters(cameraId, MinimalUpdate("Renamed") with { Source = ApiCameraSource.Usb }),
            CancellationToken.None);

        result.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        camera.Connection.Source.Should().Be(AtcSoft.VideoSurveillance.Enums.CameraSource.Network);
        camera.Display.DisplayName.Should().Be("Kept");
        storage.DidNotReceive().Save();
    }

    [Fact]
    public async Task ExecuteAsync_UsbRotationAndConnectionType_AreStored()
    {
        var storage = Substitute.For<ICameraStorageService>();
        var cameraId = Guid.NewGuid();
        var camera = new CameraConfiguration { Id = cameraId };
        camera.Display.DisplayName = "Desk";
        camera.Connection.IpAddress = "10.0.0.1";
        storage.GetCameraById(cameraId).Returns(camera);
        var handler = new UpdateCameraHandler(storage);
        var request = MinimalUpdate("Desk") with
        {
            Source = ApiCameraSource.Usb,
            UsbDeviceId = "usb#vid_046d&pid_085e",
            UsbFriendlyName = "Logitech BRIO",
            UsbWidth = 1280,
            UsbHeight = 720,
            UsbFrameRate = 30,
            UsbPixelFormat = "nv12",
            UsbCaptureAudio = true,
            UsbAudioDeviceName = "Microphone (BRIO)",
            Rotation = ApiCameraRotation.Rotate90,
            ConnectionType = ApiCameraConnectionType.Usb,
        };

        var result = await handler.ExecuteAsync(new UpdateCameraParameters(cameraId, request), CancellationToken.None);

        result.Result.Should().BeOfType<Ok<Camera>>();
        camera.Connection.Source.Should().Be(AtcSoft.VideoSurveillance.Enums.CameraSource.Usb);
        camera.Connection.Usb!.DeviceId.Should().Be("usb#vid_046d&pid_085e");
        camera.Connection.Usb.FriendlyName.Should().Be("Logitech BRIO");
        camera.Connection.Usb.Format!.Width.Should().Be(1280);
        camera.Connection.Usb.Format.FrameRate.Should().Be(30);
        camera.Connection.Usb.PreferAudio.Should().BeTrue();
        camera.Connection.Usb.AudioDeviceName.Should().Be("Microphone (BRIO)");
        camera.Display.Rotation.Should().Be(AtcSoft.VideoSurveillance.Enums.CameraRotation.Rotate90);
        camera.Connection.ConnectionType.Should().Be(AtcSoft.VideoSurveillance.Enums.CameraConnectionType.Usb);
    }

    [Fact]
    public async Task ExecuteAsync_Gives_The_Running_Analysis_The_Zones_Turned_With_The_Picture()
    {
        var storage = Substitute.For<ICameraStorageService>();
        var motion = Substitute.For<IMotionDetectionService>();
        var camera = new CameraConfiguration
        {
            MotionZones = [new() { Name = "Tree", Points = [new(0.1, 0.2), new(0.4, 0.2), new(0.4, 0.9)] }],
        };
        camera.Connection.IpAddress = "10.0.0.1";
        storage.GetCameraById(camera.Id).Returns(camera);

        await new UpdateCameraHandler(storage, motion).ExecuteAsync(
            new UpdateCameraParameters(camera.Id, MinimalUpdate("Gate") with { Rotation = ApiCameraRotation.Rotate180 }),
            CancellationToken.None);

        motion.Received(1).UseZones(
            camera.Id,
            Arg.Is<IReadOnlyList<CoreMotionZone>>(z => z.Single().Points[0] == new CoreZonePoint(0.9, 0.8)));
    }

    private static UpdateCameraRequest MinimalUpdate(string displayName)
        => new(
            DisplayName: displayName,
            Description: null!,
            Source: null,
            ConnectionType: null,
            Rotation: null,
            IpAddress: null!,
            Port: 0,
            Protocol: null,
            Path: null!,
            Username: null!,
            Password: null!,
            UsbDeviceId: null!,
            UsbFriendlyName: null!,
            UsbWidth: 0,
            UsbHeight: 0,
            UsbFrameRate: 0,
            UsbPixelFormat: null!,
            UsbCaptureAudio: false,
            UsbAudioDeviceName: null!,
            OverlayPosition: null,
            StreamUseLowLatencyMode: true,
            StreamMaxLatencyMs: 500,
            StreamRtspTransport: null,
            StreamBufferDurationMs: 0);
}