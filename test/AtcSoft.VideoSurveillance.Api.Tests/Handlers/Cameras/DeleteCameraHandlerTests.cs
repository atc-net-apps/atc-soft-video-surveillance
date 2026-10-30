namespace AtcSoft.VideoSurveillance.Api.Handlers.Cameras;

public class DeleteCameraHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_CameraExists_DeletesAndReturnsNoContent()
    {
        // Arrange
        var storage = Substitute.For<ICameraStorageService>();
        var lifecycle = Substitute.For<IUsbCameraLifecycleCoordinator>();
        var cameraId = Guid.NewGuid();
        storage.DeleteCamera(cameraId).Returns(true);
        var handler = new DeleteCameraHandler(storage, lifecycle);

        // Act
        var result = await handler.ExecuteAsync(
            new DeleteCameraParameters(cameraId),
            CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NoContent>();
        storage.Received(1).DeleteCamera(cameraId);
        storage.Received(1).Save();
    }

    [Fact]
    public async Task ExecuteAsync_CameraNotFound_ReturnsNotFound()
    {
        // Arrange
        var storage = Substitute.For<ICameraStorageService>();
        var lifecycle = Substitute.For<IUsbCameraLifecycleCoordinator>();
        var cameraId = Guid.NewGuid();
        storage.DeleteCamera(cameraId).Returns(false);
        var handler = new DeleteCameraHandler(storage, lifecycle);

        // Act
        var result = await handler.ExecuteAsync(
            new DeleteCameraParameters(cameraId),
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        storage.DidNotReceive().Save();
        lifecycle.DidNotReceive().ClearUnpluggedState(Arg.Any<Guid>());
    }

    [Fact]
    public async Task ExecuteAsync_DeletesCamera_ClearsCoordinatorUnpluggedState()
    {
        // Closes the leak flagged under §8: a USB camera unplugged then
        // deleted would leave a stale entry in
        // UsbCameraLifecycleCoordinator.unpluggedCameras until process
        // restart. Wiring ClearUnpluggedState from the delete handler
        // means the coordinator's set tracks the storage exactly.
        var storage = Substitute.For<ICameraStorageService>();
        var lifecycle = Substitute.For<IUsbCameraLifecycleCoordinator>();
        var cameraId = Guid.NewGuid();
        storage.DeleteCamera(cameraId).Returns(true);
        var handler = new DeleteCameraHandler(storage, lifecycle);

        await handler.ExecuteAsync(
            new DeleteCameraParameters(cameraId),
            CancellationToken.None);

        lifecycle.Received(1).ClearUnpluggedState(cameraId);
    }
}