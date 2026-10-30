namespace AtcSoft.VideoSurveillance.Api.Handlers.Cameras;

public class CreateCameraHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_ValidRequest_ReturnsCreatedWithMappedCamera()
    {
        // Arrange
        var storage = Substitute.For<ICameraStorageService>();
        var handler = new CreateCameraHandler(storage);
        var request = NetworkRequest(
            displayName: "Test Camera",
            description: "A test camera",
            ipAddress: "10.0.0.1",
            port: 554,
            protocol: ApiCameraProtocol.Rtsp,
            path: "stream1",
            username: "admin",
            password: "pass");
        var parameters = new CreateCameraParameters(request);

        // Act
        var result = await handler.ExecuteAsync(parameters, CancellationToken.None);

        // Assert
        var created = result.ShouldBeCreated<Camera>("/cameras/");
        created.DisplayName.Should().Be("Test Camera");
        created.IpAddress.Should().Be("10.0.0.1");
        created.Port.Should().Be(554);
        created.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_CallsStorageAddAndSave()
    {
        // Arrange
        var storage = Substitute.For<ICameraStorageService>();
        var handler = new CreateCameraHandler(storage);
        var request = NetworkRequest(
            displayName: "New Cam",
            ipAddress: "192.168.1.50",
            port: 554);
        var parameters = new CreateCameraParameters(request);

        // Act
        await handler.ExecuteAsync(parameters, CancellationToken.None);

        // Assert
        storage.Received(1).AddOrUpdateCamera(Arg.Is<CameraConfiguration>(c =>
            c.Display.DisplayName == "New Cam" &&
            c.Connection.IpAddress == "192.168.1.50"));
        storage.Received(1).Save();
    }

    [Fact]
    public async Task ExecuteAsync_UsbRequest_StoresUsbConfiguration()
    {
        var storage = Substitute.For<ICameraStorageService>();
        var handler = new CreateCameraHandler(storage);
        var request = new CreateCameraRequest(
            DisplayName: "USB Cam",
            Description: string.Empty,
            Source: ApiCameraSource.Usb,
            ConnectionType: null,
            Rotation: null,
            IpAddress: string.Empty,
            Path: string.Empty,
            Username: string.Empty,
            Password: string.Empty,
            UsbDeviceId: @"\\?\usb#vid_046d&pid_085e",
            UsbFriendlyName: "Logitech BRIO",
            UsbWidth: 1920,
            UsbHeight: 1080,
            UsbFrameRate: 30,
            UsbPixelFormat: "nv12",
            UsbCaptureAudio: false,
            UsbAudioDeviceName: string.Empty,
            OverlayPosition: null,
            StreamUseLowLatencyMode: true,
            StreamMaxLatencyMs: 500,
            StreamRtspTransport: null,
            StreamBufferDurationMs: 0,
            Port: 0);
        var parameters = new CreateCameraParameters(request);

        var result = await handler.ExecuteAsync(parameters, CancellationToken.None);

        result.ShouldBeCreated<Camera>("/cameras/");
        storage.Received(1).AddOrUpdateCamera(Arg.Is<CameraConfiguration>(c =>
            c.Connection.Source == CoreCameraSource.Usb &&
            c.Connection.Usb != null &&
            c.Connection.Usb.DeviceId == @"\\?\usb#vid_046d&pid_085e"));
    }

    // A camera that cannot be opened was stored and failed only when it was used:
    // a network camera needs an address, a USB camera a device.
    [Fact]
    public async Task ExecuteAsync_NetworkCameraWithoutAddress_IsRefused()
    {
        var storage = Substitute.For<ICameraStorageService>();
        var handler = new CreateCameraHandler(storage);

        var result = await handler.ExecuteAsync(new CreateCameraParameters(NetworkRequest("No address", ipAddress: " ")), CancellationToken.None);

        result.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        storage.DidNotReceive().AddOrUpdateCamera(Arg.Any<CameraConfiguration>());
    }

    [Fact]
    public async Task ExecuteAsync_UsbCameraWithoutDevice_IsRefused()
    {
        var storage = Substitute.For<ICameraStorageService>();
        var handler = new CreateCameraHandler(storage);
        var request = NetworkRequest("No device") with
        {
            Source = ApiCameraSource.Usb,
            UsbDeviceId = string.Empty,
            UsbFriendlyName = string.Empty,
        };

        var result = await handler.ExecuteAsync(new CreateCameraParameters(request), CancellationToken.None);

        result.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        storage.DidNotReceive().AddOrUpdateCamera(Arg.Any<CameraConfiguration>());
    }

    [Fact]
    public async Task ExecuteAsync_UsbCameraKnownByItsNameOnly_IsStored()
    {
        var storage = Substitute.For<ICameraStorageService>();
        var handler = new CreateCameraHandler(storage);
        var request = NetworkRequest("By name") with
        {
            Source = ApiCameraSource.Usb,
            UsbDeviceId = string.Empty,
            UsbFriendlyName = "HD Webcam",
        };

        var result = await handler.ExecuteAsync(new CreateCameraParameters(request), CancellationToken.None);

        result.ShouldBeCreated<Camera>("/cameras/");
    }

    private static CreateCameraRequest NetworkRequest(
        string displayName,
        string description = "",
        string ipAddress = "",
        int port = 554,
        ApiCameraProtocol protocol = ApiCameraProtocol.Rtsp,
        string path = "",
        string username = "",
        string password = "")
        => new(
            DisplayName: displayName,
            Description: description,
            Source: ApiCameraSource.Network,
            ConnectionType: null,
            Rotation: null,
            IpAddress: ipAddress,
            Protocol: protocol,
            Path: path,
            Username: username,
            Password: password,
            UsbDeviceId: string.Empty,
            UsbFriendlyName: string.Empty,
            UsbWidth: 0,
            UsbHeight: 0,
            UsbFrameRate: 0,
            UsbPixelFormat: string.Empty,
            UsbCaptureAudio: false,
            UsbAudioDeviceName: string.Empty,
            OverlayPosition: null,
            StreamUseLowLatencyMode: true,
            StreamMaxLatencyMs: 500,
            StreamRtspTransport: null,
            StreamBufferDurationMs: 0,
            Port: port);
}