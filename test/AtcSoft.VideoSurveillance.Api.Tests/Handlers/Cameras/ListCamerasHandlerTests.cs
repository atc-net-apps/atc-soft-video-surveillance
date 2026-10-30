namespace AtcSoft.VideoSurveillance.Api.Handlers.Cameras;

public class ListCamerasHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_NoCameras_ReturnsEmptyList()
    {
        // Arrange
        var storage = Substitute.For<ICameraStorageService>();
        storage.GetAllCameras().Returns([]);
        var handler = new ListCamerasHandler(storage);

        // Act
        var result = await handler.ExecuteAsync(CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<Ok<List<Camera>>>().Subject;
        okResult.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WithCameras_ReturnsMappedCameras()
    {
        // Arrange
        var storage = Substitute.For<ICameraStorageService>();
        var cam1 = new CameraConfiguration();
        cam1.Display.DisplayName = "Front Door";
        cam1.Connection.IpAddress = "192.168.1.10";
        cam1.Connection.Port = 554;

        var cam2 = new CameraConfiguration();
        cam2.Display.DisplayName = "Back Yard";
        cam2.Connection.IpAddress = "192.168.1.11";
        cam2.Connection.Port = 8080;

        storage.GetAllCameras().Returns([cam1, cam2]);
        var handler = new ListCamerasHandler(storage);

        // Act
        var result = await handler.ExecuteAsync(CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<Ok<List<Camera>>>().Subject;
        okResult.Value.Should().HaveCount(2);
        okResult.Value![0].DisplayName.Should().Be("Front Door");
        okResult.Value[0].IpAddress.Should().Be("192.168.1.10");
        okResult.Value[1].DisplayName.Should().Be("Back Yard");
        okResult.Value[1].Port.Should().Be(8080);
    }
}