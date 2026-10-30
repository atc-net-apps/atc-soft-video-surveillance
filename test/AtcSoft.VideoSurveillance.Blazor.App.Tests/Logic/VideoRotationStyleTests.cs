namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

public sealed class VideoRotationStyleTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(CameraRotation.Names.None)]
    public void An_Unrotated_Camera_Fills_Its_Tile(string? rotation)
    {
        var style = VideoRotationStyle.For(Rotation(rotation));

        style.Should().Contain("width: 100%").And.Contain("height: 100%");
        style.Should().NotContain("rotate");
    }

    [Fact]
    public void A_Half_Turn_Only_Turns_The_Picture()
    {
        var style = VideoRotationStyle.For(CameraRotation.Rotate180);

        style.Should().Contain("rotate(180deg)");
        style.Should().Contain("width: 100%").And.Contain("height: 100%");
    }

    [Theory]
    [InlineData(CameraRotation.Names.Rotate90, "rotate(90deg)")]
    [InlineData(CameraRotation.Names.Rotate270, "rotate(270deg)")]
    public void A_Quarter_Turn_Swaps_Width_And_Height_So_The_Picture_Still_Fits(
        string rotation,
        string expectedTransform)
    {
        var style = VideoRotationStyle.For(Rotation(rotation));

        // The element is laid out with the tile's height as its width and
        // the other way round, then turned around the tile's centre.
        style.Should().Contain("width: 100cqh").And.Contain("height: 100cqw");
        style.Should().Contain("translate(-50%, -50%)").And.Contain(expectedTransform);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData(CameraRotation.Names.None, false)]
    [InlineData(CameraRotation.Names.Rotate90, true)]
    [InlineData(CameraRotation.Names.Rotate180, true)]
    [InlineData(CameraRotation.Names.Rotate270, true)]
    public void IsRotated_Tells_Whether_The_Browser_Controls_Would_Be_Turned_Too(
        string? rotation,
        bool expected)
        => VideoRotationStyle.IsRotated(Rotation(rotation)).Should().Be(expected);

    // A struct cannot be an attribute argument, so the cases name the value on the wire.
    private static CameraRotation? Rotation(string? name)
        => name is null ? null : new CameraRotation(name);
}