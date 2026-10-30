namespace AtcSoft.VideoSurveillance.Api.Handlers.Layouts;

public class UpdateLayoutHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_LayoutExists_UpdatesNameAndReturnsOk()
    {
        // Arrange
        var storage = Substitute.For<ICameraStorageService>();
        var layoutId = Guid.NewGuid();
        var layout = new CameraLayout
        {
            Id = layoutId,
            Name = "Old Name",
            Items = [new CameraLayoutItem { OrderNumber = 0 }],
        };
        storage.GetLayoutById(layoutId).Returns(layout);
        var handler = new UpdateLayoutHandler(storage);

        var request = new UpdateLayoutRequest(
            Name: "New Name",
            Rows: 0,
            Columns: 0,
            Cameras: null!);

        // Act
        var result = await handler.ExecuteAsync(
            new UpdateLayoutParameters(layoutId, request),
            CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<Ok<Layout>>().Subject;
        okResult.Value!.Name.Should().Be("New Name");
        storage.Received(1).AddOrUpdateLayout(layout);
        storage.Received(1).Save();
    }

    [Fact]
    public async Task ExecuteAsync_LayoutExists_UpdatesCameraList()
    {
        // Arrange
        var storage = Substitute.For<ICameraStorageService>();
        var layoutId = Guid.NewGuid();
        var camId1 = Guid.NewGuid();
        var camId2 = Guid.NewGuid();
        var layout = new CameraLayout
        {
            Id = layoutId,
            Name = "Grid",
            Items = [new CameraLayoutItem { OrderNumber = 0 }],
        };
        storage.GetLayoutById(layoutId).Returns(layout);
        var handler = new UpdateLayoutHandler(storage);

        var request = new UpdateLayoutRequest(
            Name: null!,
            Rows: 0,
            Columns: 0,
            Cameras: [new LayoutItem(camId1, 0), new LayoutItem(camId2, 1)]);

        // Act
        var result = await handler.ExecuteAsync(
            new UpdateLayoutParameters(layoutId, request),
            CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<Ok<Layout>>().Subject;
        okResult.Value!.Cameras.Should().HaveCount(2);
        okResult.Value.Cameras[0].CameraId.Should().Be(camId1);
        okResult.Value.Cameras[1].CameraId.Should().Be(camId2);
    }

    [Fact]
    public async Task ExecuteAsync_LayoutNotFound_ReturnsNotFound()
    {
        // Arrange
        var storage = Substitute.For<ICameraStorageService>();
        var layoutId = Guid.NewGuid();
        storage.GetLayoutById(layoutId).Returns((CameraLayout?)null);
        var handler = new UpdateLayoutHandler(storage);

        var request = new UpdateLayoutRequest(
            Name: "Name",
            Rows: 0,
            Columns: 0,
            Cameras: null!);

        // Act
        var result = await handler.ExecuteAsync(
            new UpdateLayoutParameters(layoutId, request),
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }
}