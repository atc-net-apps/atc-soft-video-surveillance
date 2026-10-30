namespace AtcSoft.VideoSurveillance.Models;

public class CameraLayoutTests
{
    [Fact]
    public void New_Instance_Has_Default_Values()
    {
        // Act
        var layout = new CameraLayout();

        // Assert
        layout.Id.Should().NotBeEmpty();
        layout.Name.Should().BeEmpty();
        layout.Items.Should().NotBeNull().And.BeEmpty();
        layout.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        layout.ModifiedAt.Should().BeNull();
    }
}