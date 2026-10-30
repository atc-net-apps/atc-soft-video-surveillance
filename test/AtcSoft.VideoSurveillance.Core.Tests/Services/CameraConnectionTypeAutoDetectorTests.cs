namespace AtcSoft.VideoSurveillance.Services;

public class CameraConnectionTypeAutoDetectorTests
{
    [Fact]
    public async Task OnCameraConnected_Fills_None_And_Persists()
    {
        var camera = NetworkCamera(CameraConnectionType.None);
        var storage = StorageWith(camera);
        var sut = new CameraConnectionTypeAutoDetector(DetectorReturning(CameraConnectionType.Wifi), storage);

        await sut.OnCameraConnectedAsync(camera);

        camera.Connection.ConnectionType.Should().Be(CameraConnectionType.Wifi);
        storage.Received(1).AddOrUpdateCamera(camera);
    }

    [Fact]
    public async Task OnCameraConnected_Skips_Camera_With_A_Value()
    {
        var camera = NetworkCamera(CameraConnectionType.Lan);
        var detector = DetectorReturning(CameraConnectionType.Wifi);
        var sut = new CameraConnectionTypeAutoDetector(detector, StorageWith(camera));

        await sut.OnCameraConnectedAsync(camera);

        detector.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task OnCameraConnected_Skips_Usb_Camera()
    {
        var camera = NetworkCamera(CameraConnectionType.None);
        camera.Connection.Source = CameraSource.Usb;
        var detector = DetectorReturning(CameraConnectionType.Wifi);
        var sut = new CameraConnectionTypeAutoDetector(detector, StorageWith(camera));

        await sut.OnCameraConnectedAsync(camera);

        detector.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task OnCameraConnected_Probes_Once_Per_Camera()
    {
        var camera = NetworkCamera(CameraConnectionType.None);
        var detector = DetectorReturning(CameraConnectionType.None);
        var sut = new CameraConnectionTypeAutoDetector(detector, StorageWith(camera));

        await sut.OnCameraConnectedAsync(camera);
        await sut.OnCameraConnectedAsync(camera);

        await detector.Received(1).DetectAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task OnCameraConnected_Does_Not_Overwrite_Value_Set_Meanwhile()
    {
        var camera = NetworkCamera(CameraConnectionType.None);
        var stored = NetworkCamera(CameraConnectionType.Lan);
        stored.Id = camera.Id;
        var storage = StorageWith(stored);
        var sut = new CameraConnectionTypeAutoDetector(DetectorReturning(CameraConnectionType.Wifi), storage);

        await sut.OnCameraConnectedAsync(camera);

        stored.Connection.ConnectionType.Should().Be(CameraConnectionType.Lan);
        storage.DidNotReceive().AddOrUpdateCamera(Arg.Any<CameraConfiguration>());
    }

    private static CameraConfiguration NetworkCamera(
        CameraConnectionType connectionType)
    {
        var camera = new CameraConfiguration();
        camera.Connection.IpAddress = "10.0.0.5";
        camera.Connection.ConnectionType = connectionType;
        camera.Authentication.UserName = "admin";
        return camera;
    }

    private static ICameraStorageService StorageWith(CameraConfiguration camera)
    {
        var storage = Substitute.For<ICameraStorageService>();
        storage.GetCameraById(camera.Id).Returns(camera);
        return storage;
    }

    private static ICameraConnectionTypeDetector DetectorReturning(
        CameraConnectionType connectionType)
    {
        var detector = Substitute.For<ICameraConnectionTypeDetector>();
        detector
            .DetectAsync(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(connectionType);
        return detector;
    }
}