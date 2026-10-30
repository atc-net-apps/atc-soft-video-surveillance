namespace AtcSoft.VideoSurveillance.Api.Handlers.Layouts;

public class DeleteLayoutHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_LayoutExists_DeletesAndReturnsNoContent()
    {
        // Arrange
        var storage = Substitute.For<ICameraStorageService>();
        var layoutId = Guid.NewGuid();
        storage.DeleteLayout(layoutId).Returns(true);
        var handler = new DeleteLayoutHandler(storage);

        // Act
        var result = await handler.ExecuteAsync(
            new DeleteLayoutParameters(layoutId),
            CancellationToken.None);

        // Assert
        result.Result.Should().BeOfType<NoContent>();
        storage.Received(1).DeleteLayout(layoutId);
        storage.Received(1).Save();
    }

    [Fact]
    public async Task ExecuteAsync_LayoutNotFound_ReturnsNotFound()
    {
        // Arrange
        var storage = Substitute.For<ICameraStorageService>();
        var layoutId = Guid.NewGuid();
        storage.DeleteLayout(layoutId).Returns(false);
        var handler = new DeleteLayoutHandler(storage);

        // Act
        var result = await handler.ExecuteAsync(
            new DeleteLayoutParameters(layoutId),
            CancellationToken.None);

        // Assert
        result.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        storage.DidNotReceive().Save();
    }
}