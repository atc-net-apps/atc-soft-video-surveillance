namespace AtcSoft.VideoSurveillance.Api.Handlers.Layouts;

public class ListLayoutsHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_NoLayouts_ReturnsEmptyList()
    {
        // Arrange
        var storage = Substitute.For<ICameraStorageService>();
        storage.GetAllLayouts().Returns([]);
        var handler = new ListLayoutsHandler(storage);

        // Act
        var result = await handler.ExecuteAsync(CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<Ok<List<Layout>>>().Subject;
        okResult.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WithLayouts_ReturnsMappedLayouts()
    {
        // Arrange
        var storage = Substitute.For<ICameraStorageService>();
        var layout = new CameraLayout
        {
            Name = "2x2 Grid",
            Items =
            [
                new CameraLayoutItem { OrderNumber = 0 },
                new CameraLayoutItem { OrderNumber = 1 },
                new CameraLayoutItem { OrderNumber = 2 },
                new CameraLayoutItem { OrderNumber = 3 },
            ],
        };
        storage.GetAllLayouts().Returns([layout]);
        var handler = new ListLayoutsHandler(storage);

        // Act
        var result = await handler.ExecuteAsync(CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<Ok<List<Layout>>>().Subject;
        okResult.Value.Should().HaveCount(1);
        okResult.Value![0].Name.Should().Be("2x2 Grid");
        okResult.Value[0].Rows.Should().Be(2);
        okResult.Value[0].Columns.Should().Be(2);
    }
}