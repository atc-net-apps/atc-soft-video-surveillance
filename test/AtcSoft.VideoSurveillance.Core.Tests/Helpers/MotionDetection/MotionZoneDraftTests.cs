namespace AtcSoft.VideoSurveillance.Helpers.MotionDetection;

/// <summary>
/// A camera's zones in an open form: turned with the rotation chosen there, and changed only when drawn.
/// </summary>
public sealed class MotionZoneDraftTests
{
    [Fact]
    public void A_Rotation_Chosen_In_The_Form_Turns_The_Zones_With_The_Picture()
    {
        var draft = new MotionZoneDraft([Zone(0.1, 0.2)], CameraRotation.None);

        draft.Turn(CameraRotation.Rotate90);

        draft.Zones[0].Points[0].X.Should().BeApproximately(0.8, 1e-9);
        draft.Zones[0].Points[0].Y.Should().BeApproximately(0.1, 1e-9);
        draft.IsChanged.Should().BeFalse("the server turns the stored zones the same way");
    }

    [Fact]
    public void Turning_Back_And_Forth_Gives_The_Same_Corners_Again()
    {
        // 0.1 turned twice by a half is 0.09999999999999998 when each turn starts from the last.
        var draft = new MotionZoneDraft([Zone(0.1, 0.2)], CameraRotation.None);

        foreach (var rotation in new[] { CameraRotation.Rotate90, CameraRotation.Rotate270, CameraRotation.Rotate180, CameraRotation.None })
        {
            draft.Turn(rotation);
        }

        draft.Zones[0].Points[0].Should().Be(new ZonePoint(0.1, 0.2));
        draft.IsChanged.Should().BeFalse();
    }

    [Fact]
    public void Zones_Drawn_At_One_Rotation_Turn_From_There()
    {
        var draft = new MotionZoneDraft([], CameraRotation.None);
        draft.Turn(CameraRotation.Rotate180);

        draft.Change([Zone(0.1, 0.2)]);
        draft.Turn(CameraRotation.None);

        draft.Zones[0].Points[0].X.Should().BeApproximately(0.9, 1e-9);
        draft.Zones[0].Points[0].Y.Should().BeApproximately(0.8, 1e-9);
        draft.IsChanged.Should().BeTrue();
    }

    [Fact]
    public void The_Stored_Zones_Are_Copied()
    {
        var stored = Zone(0.1, 0.2);
        var draft = new MotionZoneDraft([stored], CameraRotation.None);

        stored.Name = "Changed";
        stored.Points[0] = new ZonePoint(0.5, 0.5);

        draft.IsChanged.Should().BeFalse("a change to the stored zones does not reach the draft");
        draft.Zones[0].Name.Should().Be("Tree");
        draft.Zones[0].Should().NotBeSameAs(stored);
    }

    [Fact]
    public void A_Zone_Removed_Is_A_Change()
    {
        var draft = new MotionZoneDraft([Zone(0.1, 0.2)], CameraRotation.Rotate90);

        draft.Change([]);

        draft.IsChanged.Should().BeTrue();
        draft.Zones.Should().BeEmpty();
    }

    private static MotionZone Zone(
        double x,
        double y)
        => new()
        {
            Name = "Tree",
            Points = [new(x, y), new(0.5, 0.2), new(0.5, 0.6)],
        };
}