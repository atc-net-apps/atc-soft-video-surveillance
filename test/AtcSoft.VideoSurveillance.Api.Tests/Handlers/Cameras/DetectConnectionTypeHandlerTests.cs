namespace AtcSoft.VideoSurveillance.Api.Handlers.Cameras;

public class DetectConnectionTypeHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_NetworkCamera_ReturnsDetectedType_WithStoredCredentials()
    {
        // Arrange
        var cameraId = Guid.NewGuid();
        var camera = new CameraConfiguration { Id = cameraId };
        camera.Connection.IpAddress = "10.0.0.5";
        camera.Authentication.UserName = "admin";
        camera.Authentication.Password = "secret";
        var storage = Substitute.For<ICameraStorageService>();
        storage.GetCameraById(cameraId).Returns(camera);
        var detector = Substitute.For<ICameraConnectionTypeDetector>();
        detector
            .DetectAsync("10.0.0.5", "admin", "secret", Arg.Any<CancellationToken>())
            .Returns(CoreCameraConnectionType.Wifi);
        var handler = new DetectConnectionTypeHandler(storage, detector);

        // Act
        var result = await handler.ExecuteAsync(
            new DetectConnectionTypeParameters(cameraId),
            CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<Ok<ConnectionTypeDetection>>().Subject;
        okResult.Value!.ConnectionType.Should().Be(ApiCameraConnectionType.Wifi);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotSaveTheResult()
    {
        // Arrange
        var cameraId = Guid.NewGuid();
        var camera = new CameraConfiguration { Id = cameraId };
        camera.Connection.IpAddress = "10.0.0.5";
        var storage = Substitute.For<ICameraStorageService>();
        storage.GetCameraById(cameraId).Returns(camera);
        var detector = Substitute.For<ICameraConnectionTypeDetector>();
        detector
            .DetectAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(CoreCameraConnectionType.Lan);
        var handler = new DetectConnectionTypeHandler(storage, detector);

        // Act
        await handler.ExecuteAsync(
            new DetectConnectionTypeParameters(cameraId),
            CancellationToken.None);

        // Assert
        camera.Connection.ConnectionType.Should().Be(CoreCameraConnectionType.None);
        storage.DidNotReceive().AddOrUpdateCamera(Arg.Any<CameraConfiguration>());
    }

    [Fact]
    public async Task ExecuteAsync_UsbCamera_ReturnsUsb_WithoutProbing()
    {
        // Arrange
        var cameraId = Guid.NewGuid();
        var camera = new CameraConfiguration { Id = cameraId };
        camera.Connection.Source = CoreCameraSource.Usb;
        var storage = Substitute.For<ICameraStorageService>();
        storage.GetCameraById(cameraId).Returns(camera);
        var detector = Substitute.For<ICameraConnectionTypeDetector>();
        var handler = new DetectConnectionTypeHandler(storage, detector);

        // Act
        var result = await handler.ExecuteAsync(
            new DetectConnectionTypeParameters(cameraId),
            CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<Ok<ConnectionTypeDetection>>().Subject;
        okResult.Value!.ConnectionType.Should().Be(ApiCameraConnectionType.Usb);
        detector.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_CameraNotFound_ReturnsNotFound()
    {
        // Arrange
        var cameraId = Guid.NewGuid();
        var storage = Substitute.For<ICameraStorageService>();
        storage.GetCameraById(cameraId).Returns((CameraConfiguration?)null);
        var handler = new DetectConnectionTypeHandler(storage, Substitute.For<ICameraConnectionTypeDetector>());

        // Act
        var result = await handler.ExecuteAsync(
            new DetectConnectionTypeParameters(cameraId),
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }
}