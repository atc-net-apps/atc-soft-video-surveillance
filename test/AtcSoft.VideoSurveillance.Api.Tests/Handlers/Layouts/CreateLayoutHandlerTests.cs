namespace AtcSoft.VideoSurveillance.Api.Handlers.Layouts;

public class CreateLayoutHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_ValidRequest_ReturnsCreatedWithLayout()
    {
        // Arrange
        var storage = Substitute.For<ICameraStorageService>();
        var handler = new CreateLayoutHandler(storage);
        var request = new CreateLayoutRequest(Name: "My Layout", Rows: 3, Columns: 3);
        var parameters = new CreateLayoutParameters(request);

        // Act
        var result = await handler.ExecuteAsync(parameters, CancellationToken.None);

        // Assert
        var created = result.ShouldBeCreated<Layout>("/layouts/");
        created.Name.Should().Be("My Layout");
        created.Id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_ValidRequest_AllocatesCorrectNumberOfItems()
    {
        // Arrange
        var storage = Substitute.For<ICameraStorageService>();
        var handler = new CreateLayoutHandler(storage);
        var request = new CreateLayoutRequest(Name: "3x2 Grid", Rows: 3, Columns: 2);
        var parameters = new CreateLayoutParameters(request);

        // Act
        var result = await handler.ExecuteAsync(parameters, CancellationToken.None);

        // Assert
        var created = result.ShouldBeCreated<Layout>("/layouts/");
        created.Cameras.Should().HaveCount(6);
        storage.Received(1).AddOrUpdateLayout(Arg.Is<CameraLayout>(l =>
            l.Items.Count == 6 && l.Name == "3x2 Grid"));
        storage.Received(1).Save();
    }
}