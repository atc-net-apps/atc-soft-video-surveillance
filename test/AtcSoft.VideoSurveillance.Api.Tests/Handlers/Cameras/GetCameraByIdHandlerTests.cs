namespace AtcSoft.VideoSurveillance.Api.Handlers.Cameras;

public class GetCameraByIdHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_CameraExists_ReturnsOkWithCamera()
    {
        // Arrange
        var storage = Substitute.For<ICameraStorageService>();
        var cameraId = Guid.NewGuid();
        var camera = new CameraConfiguration { Id = cameraId };
        camera.Display.DisplayName = "Lobby Camera";
        camera.Connection.IpAddress = "192.168.1.100";
        storage.GetCameraById(cameraId).Returns(camera);
        var handler = new GetCameraByIdHandler(storage);

        // Act
        var result = await handler.ExecuteAsync(
            new GetCameraByIdParameters(cameraId),
            CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<Ok<Camera>>().Subject;
        okResult.Value.Should().NotBeNull();
        okResult.Value!.Id.Should().Be(cameraId);
        okResult.Value.DisplayName.Should().Be("Lobby Camera");
    }

    [Fact]
    public async Task ExecuteAsync_CameraNotFound_ReturnsNotFound()
    {
        // Arrange
        var storage = Substitute.For<ICameraStorageService>();
        var cameraId = Guid.NewGuid();
        storage.GetCameraById(cameraId).Returns((CameraConfiguration?)null);
        var handler = new GetCameraByIdHandler(storage);

        // Act
        var result = await handler.ExecuteAsync(
            new GetCameraByIdParameters(cameraId),
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }
}