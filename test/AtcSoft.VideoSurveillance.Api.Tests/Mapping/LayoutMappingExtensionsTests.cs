namespace AtcSoft.VideoSurveillance.Api.Mapping;

public class LayoutMappingExtensionsTests
{
    [Fact]
    public void ToApiModel_MapsFieldsCorrectly()
    {
        // Arrange
        var cameraId = Guid.NewGuid();
        var core = new CameraLayout
        {
            Id = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Name = "Test Layout",
            Items =
            [
                new CameraLayoutItem { CameraId = cameraId, OrderNumber = 0 },
                new CameraLayoutItem { OrderNumber = 1 },
                new CameraLayoutItem { OrderNumber = 2 },
                new CameraLayoutItem { OrderNumber = 3 },
            ],
        };

        // Act
        var api = core.ToApiModel();

        // Assert
        api.Id.Should().Be(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        api.Name.Should().Be("Test Layout");
        api.Cameras.Should().HaveCount(4);
        api.Cameras[0].CameraId.Should().Be(cameraId);
        api.Cameras[0].Position.Should().Be(0);
    }

    [Theory]
    [InlineData(1, 1, 1)]
    [InlineData(4, 2, 2)]
    [InlineData(6, 3, 2)]
    [InlineData(9, 3, 3)]
    [InlineData(0, 1, 1)]
    public void ToApiModel_ComputesGridDimensionsCorrectly(
        int itemCount,
        int expectedRows,
        int expectedColumns)
    {
        // Arrange
        var core = new CameraLayout { Name = "Grid" };
        for (var i = 0; i < itemCount; i++)
        {
            core.Items.Add(new CameraLayoutItem { OrderNumber = i });
        }

        // Act
        var api = core.ToApiModel();

        // Assert
        api.Rows.Should().Be(expectedRows);
        api.Columns.Should().Be(expectedColumns);
    }

    [Fact]
    public void ToCoreModel_AllocatesItemsFromRowsAndColumns()
    {
        // Arrange
        var request = new CreateLayoutRequest(Name: "3x3 Grid", Rows: 3, Columns: 3);

        // Act
        var core = request.ToCoreModel();

        // Assert
        core.Name.Should().Be("3x3 Grid");
        core.Items.Should().HaveCount(9);
        core.Items[0].OrderNumber.Should().Be(0);
        core.Items[8].OrderNumber.Should().Be(8);
        core.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void ApplyUpdate_WithName_UpdatesName()
    {
        // Arrange
        var core = new CameraLayout { Name = "Old" };

        var request = new UpdateLayoutRequest(
            Name: "New",
            Rows: 0,
            Columns: 0,
            Cameras: null!);

        // Act
        core.ApplyUpdate(request);

        // Assert
        core.Name.Should().Be("New");
        core.ModifiedAt.Should().NotBeNull();
    }

    [Fact]
    public void ApplyUpdate_WithCameras_ReplacesItemsList()
    {
        // Arrange
        var camId = Guid.NewGuid();
        var core = new CameraLayout
        {
            Name = "Grid",
            Items = [new CameraLayoutItem { OrderNumber = 0 }],
        };

        var request = new UpdateLayoutRequest(
            Name: null!,
            Rows: 0,
            Columns: 0,
            Cameras: [new LayoutItem(camId, 0), new LayoutItem(Guid.Empty, 1)]);

        // Act
        core.ApplyUpdate(request);

        // Assert
        core.Items.Should().HaveCount(2);
        core.Items[0].CameraId.Should().Be(camId);
        core.Items[1].OrderNumber.Should().Be(1);
    }

    [Fact]
    public void ApplyUpdate_WithRowsAndColumns_ExpandsItems()
    {
        // Arrange
        var core = new CameraLayout
        {
            Name = "Small",
            Items = [new CameraLayoutItem { OrderNumber = 0 }],
        };

        var request = new UpdateLayoutRequest(
            Name: null!,
            Rows: 2,
            Columns: 2,
            Cameras: null!);

        // Act
        core.ApplyUpdate(request);

        // Assert
        core.Items.Should().HaveCount(4);
    }
}