namespace AtcSoft.VideoSurveillance.Models;

public class BoundingBoxTests
{
    [Fact]
    public void Properties_Can_Be_Set()
    {
        // Act
        var box = new BoundingBox
        {
            X = 10,
            Y = 20,
            Width = 100,
            Height = 200,
        };

        // Assert
        box.X.Should().Be(10);
        box.Y.Should().Be(20);
        box.Width.Should().Be(100);
        box.Height.Should().Be(200);
    }

    [Fact]
    public void Default_Instance_Has_Zero_Values()
    {
        // Act
        var box = new BoundingBox();

        // Assert
        box.X.Should().Be(0);
        box.Y.Should().Be(0);
        box.Width.Should().Be(0);
        box.Height.Should().Be(0);
    }

    [Fact]
    public void Area_Returns_Width_Times_Height()
    {
        // Arrange
        var box = new BoundingBox
        {
            Width = 10,
            Height = 20,
        };

        // Act & Assert
        box.Area.Should().Be(200);
    }

    [Fact]
    public void Area_Returns_Zero_For_Default_Instance()
    {
        // Arrange
        var box = new BoundingBox();

        // Act & Assert
        box.Area.Should().Be(0);
    }
}