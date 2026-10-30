namespace AtcSoft.VideoSurveillance.Api.Handlers.Cameras;

public class StopRecordingHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_CameraExists_StopsRecordingAndReturnsStatus()
    {
        // Arrange
        var storage = Substitute.For<ICameraStorageService>();
        var recordingService = Substitute.For<IRecordingService>();
        var cameraId = Guid.NewGuid();
        var camera = new CameraConfiguration { Id = cameraId };
        storage.GetCameraById(cameraId).Returns(camera);
        recordingService.GetRecordingState(cameraId).Returns(RecordingState.Idle);
        var handler = new StopRecordingHandler(storage, recordingService);

        // Act
        var result = await handler.ExecuteAsync(
            new StopRecordingParameters(cameraId),
            CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<Ok<RecordingStatus>>().Subject;
        okResult.Value.Should().NotBeNull();
        okResult.Value!.CameraId.Should().Be(cameraId);
        okResult.Value.State.Should().Be(RecordingStatusState.Idle);
        recordingService.Received(1).StopRecording(cameraId);
    }

    [Fact]
    public async Task ExecuteAsync_CameraNotFound_ReturnsNotFound()
    {
        // Arrange
        var storage = Substitute.For<ICameraStorageService>();
        var recordingService = Substitute.For<IRecordingService>();
        var cameraId = Guid.NewGuid();
        storage.GetCameraById(cameraId).Returns((CameraConfiguration?)null);
        var handler = new StopRecordingHandler(storage, recordingService);

        // Act
        var result = await handler.ExecuteAsync(
            new StopRecordingParameters(cameraId),
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        recordingService.DidNotReceive().StopRecording(Arg.Any<Guid>());
    }
}