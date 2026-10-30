namespace AtcSoft.VideoSurveillance.Api.Handlers.Layouts;

public class ApplyLayoutHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_LayoutExists_SetsStartupAndReturnsOk()
    {
        // Arrange
        var storage = Substitute.For<ICameraStorageService>();
        var layoutId = Guid.NewGuid();
        var layout = new CameraLayout
        {
            Id = layoutId,
            Name = "Main View",
            Items = [new CameraLayoutItem { OrderNumber = 0 }],
        };
        storage.GetLayoutById(layoutId).Returns(layout);
        var handler = new ApplyLayoutHandler(storage);

        // Act
        var result = await handler.ExecuteAsync(
            new ApplyLayoutParameters(layoutId),
            CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<Ok<Layout>>().Subject;
        okResult.Value!.Name.Should().Be("Main View");
        storage.StartupLayoutId.Should().Be(layoutId);
        storage.Received(1).Save();
    }

    [Fact]
    public async Task ExecuteAsync_LayoutNotFound_ReturnsNotFound()
    {
        // Arrange
        var storage = Substitute.For<ICameraStorageService>();
        var layoutId = Guid.NewGuid();
        storage.GetLayoutById(layoutId).Returns((CameraLayout?)null);
        var handler = new ApplyLayoutHandler(storage);

        // Act
        var result = await handler.ExecuteAsync(
            new ApplyLayoutParameters(layoutId),
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }
}