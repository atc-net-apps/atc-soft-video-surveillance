namespace AtcSoft.VideoSurveillance.Helpers.MotionDetection;

/// <summary>
/// The zones turn with the picture, so they stay on the same part of the scene.
/// </summary>
public sealed class MotionZoneGeometryTests
{
    [Theory]
    [InlineData(CameraRotation.None, CameraRotation.Rotate90, 0.8, 0.1)]
    [InlineData(CameraRotation.None, CameraRotation.Rotate180, 0.9, 0.8)]
    [InlineData(CameraRotation.None, CameraRotation.Rotate270, 0.2, 0.9)]
    [InlineData(CameraRotation.Rotate90, CameraRotation.None, 0.2, 0.9)]
    [InlineData(CameraRotation.Rotate270, CameraRotation.Rotate90, 0.9, 0.8)]
    [InlineData(CameraRotation.Rotate180, CameraRotation.Rotate180, 0.1, 0.2)]
    public void A_Corner_Turns_With_The_Picture(
        CameraRotation from,
        CameraRotation to,
        double x,
        double y)
    {
        // A corner near the top left of the picture: x 0.1, y 0.2.
        var turned = MotionZoneGeometry.Turn([Zone((0.1, 0.2))], from, to);

        turned[0].Points[0].X.Should().BeApproximately(x, 1e-9);
        turned[0].Points[0].Y.Should().BeApproximately(y, 1e-9);
    }

    [Fact]
    public void The_Same_Pixels_Are_Masked_After_A_Turn()
    {
        // A 6 x 4 picture turned a quarter clockwise is 4 x 6: pixel (x, y) moves to (3 - y, x).
        var zone = Zone((0, 0), (0.5, 0), (0.5, 0.75), (0, 0.75));
        var before = ZoneMask.Ignored([zone], 6, 4)!;
        var after = ZoneMask.Ignored(MotionZoneGeometry.Turn([zone], CameraRotation.None, CameraRotation.Rotate90), 4, 6)!;

        for (var y = 0; y < 4; y++)
        {
            for (var x = 0; x < 6; x++)
            {
                after[(x * 4) + (3 - y)].Should().Be(before[(y * 6) + x], $"pixel ({x}, {y})");
            }
        }
    }

    [Fact]
    public void The_Zones_Given_Are_Left_As_They_Are()
    {
        var zone = Zone((0.1, 0.2), (0.3, 0.2), (0.3, 0.4));

        MotionZoneGeometry.Turn([zone], CameraRotation.None, CameraRotation.Rotate90);

        zone.Points[0].Should().Be(new ZonePoint(0.1, 0.2));
    }

    private static MotionZone Zone(params (double X, double Y)[] points)
        => new() { Kind = MotionZoneKind.Ignore, Points = [.. points.Select(p => new ZonePoint(p.X, p.Y))] };
}