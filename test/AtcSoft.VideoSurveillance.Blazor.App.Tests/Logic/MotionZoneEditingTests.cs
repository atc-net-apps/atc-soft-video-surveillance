namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

using CoreDraft = AtcSoft.VideoSurveillance.Helpers.MotionDetection.MotionZoneDraft;
using CoreEditing = AtcSoft.VideoSurveillance.Helpers.MotionDetection.MotionZoneEditing;
using CoreGeometry = AtcSoft.VideoSurveillance.Helpers.MotionDetection.MotionZoneGeometry;
using CoreRotation = AtcSoft.VideoSurveillance.Enums.CameraRotation;
using CoreZone = AtcSoft.VideoSurveillance.Models.MotionZone;
using CoreZonePoint = AtcSoft.VideoSurveillance.Models.ZonePoint;

/// <summary>
/// The web zone editor's copy of Core's editing, kept equal to the original.
/// </summary>
public sealed class MotionZoneEditingTests
{
    private static readonly (double X, double Y)[] Shape = [(0.2, 0.2), (0.6, 0.15), (0.7, 0.6), (0.4, 0.4), (0.2, 0.6)];

    private static readonly (double X, double Y)[] Pointers =
    [
        (0, 0), (0.2, 0.2), (0.21, 0.205), (0.4, 0.17), (0.4, 0.3), (0.4, 0.45), (0.65, 0.4), (0.69, 0.58), (0.2, 0.4), (0.9, 0.9), (1, 1),
    ];

    private static readonly (string Web, CoreRotation Core)[] Rotations =
    [
        (CameraRotation.Names.None, CoreRotation.None),
        (CameraRotation.Names.Rotate90, CoreRotation.Rotate90),
        (CameraRotation.Names.Rotate180, CoreRotation.Rotate180),
        (CameraRotation.Names.Rotate270, CoreRotation.Rotate270),
    ];

    [Fact]
    public void The_Limits_And_Sizes_Are_Cores()
    {
        MotionZoneEditing.MaxZones.Should().Be(CoreEditing.MaxZones);
        MotionZoneEditing.MinCorners.Should().Be(CoreEditing.MinCorners);
        MotionZoneEditing.MaxCorners.Should().Be(CoreEditing.MaxCorners);
        MotionZoneEditing.MaxNameLength.Should().Be(CoreEditing.MaxNameLength);
        MotionZoneEditing.DefaultAspectRatio.Should().Be(CoreEditing.DefaultAspectRatio);
        MotionZoneEditing.GrabDistance.Should().Be(CoreEditing.GrabDistance);
        foreach (var culture in new[] { "en-US", "da-DK" })
        {
            foreach (var text in new[] { "", "5", "105", "-1", "0.05", "0,05", "x" })
            {
                var format = CultureInfo.GetCultureInfo(culture);
                MotionZoneEditing.TryReadSensitivity(text, format, out var webSensitivity).Should().Be(CoreEditing.TryReadSensitivity(text, format, out var coreSensitivity));
                webSensitivity.Should().Be(coreSensitivity);
                MotionZoneEditing.TryReadSmallestObjectPercent(text, format, out var webSmallest).Should().Be(CoreEditing.TryReadSmallestObjectPercent(text, format, out var coreSmallest));
                webSmallest.Should().Be(coreSmallest);
            }
        }

        for (var count = 0; count <= 33; count++)
        {
            MotionZoneEditing.CanAddZone(count).Should().Be(CoreEditing.CanAddZone(count));
            MotionZoneEditing.CanAddCorner(count).Should().Be(CoreEditing.CanAddCorner(count));
            MotionZoneEditing.CanRemoveCorner(count).Should().Be(CoreEditing.CanRemoveCorner(count));
            Pairs(MotionZoneEditing.NewZoneCorners(count)).Should().Equal(Pairs(CoreEditing.NewZoneCorners(count)));
        }

        string Name(int n) => $"Zone {n}";
        MotionZoneEditing.NextNumber(["Zone 1", "zone 3"], Name).Should().Be(CoreEditing.NextNumber(["Zone 1", "zone 3"], Name));
    }

    [Fact]
    public void Corners_Are_Moved_Added_And_Removed_As_In_Core()
    {
        var web = Web(Shape);
        var core = Core(Shape);

        for (var i = 0; i < Shape.Length; i++)
        {
            Pairs(MotionZoneEditing.MoveCorner(web, i, 1.3, 0.4)).Should().Equal(Pairs(CoreEditing.MoveCorner(core, i, 1.3, 0.4)));
            Pairs(MotionZoneEditing.AddCorner(web, i)).Should().Equal(Pairs(CoreEditing.AddCorner(core, i)));
            Pairs(MotionZoneEditing.RemoveCorner(web, i)).Should().Equal(Pairs(CoreEditing.RemoveCorner(core, i)));
        }

        foreach (var (dx, dy) in new[] { (0.1, 0.1), (0.9, -0.9), (-0.05, 0.3) })
        {
            Pairs(MotionZoneEditing.MoveZone(web, dx, dy)).Should().Equal(Pairs(CoreEditing.MoveZone(core, dx, dy)));
        }

        MotionZoneEditing.LongestEdge(web, 1600, 900).Should().Be(CoreEditing.LongestEdge(core, 1600, 900));
        MotionZoneEditing.LongestEdge(web, 300, 900).Should().Be(CoreEditing.LongestEdge(core, 300, 900));
    }

    [Fact]
    public void The_Pointer_Finds_What_It_Finds_In_Core()
    {
        var web = Web(Shape);
        var core = Core(Shape);
        IReadOnlyList<IReadOnlyList<ZonePoint>> webZones = [web, Web([(0.5, 0.5), (0.9, 0.5), (0.9, 0.9)])];
        IReadOnlyList<IReadOnlyList<CoreZonePoint>> coreZones = [core, Core([(0.5, 0.5), (0.9, 0.5), (0.9, 0.9)])];

        foreach (var (x, y) in Pointers)
        {
            MotionZoneEditing.FindCorner(web, x, y, 640, 360).Should().Be(CoreEditing.FindCorner(core, x, y, 640, 360), $"corner at ({x}, {y})");
            MotionZoneEditing.FindEdge(web, x, y, 640, 360).Should().Be(CoreEditing.FindEdge(core, x, y, 640, 360), $"edge at ({x}, {y})");
            MotionZoneEditing.Contains(web, x, y).Should().Be(CoreEditing.Contains(core, x, y), $"inside at ({x}, {y})");
            MotionZoneEditing.FindZone(webZones, x, y).Should().Be(CoreEditing.FindZone(coreZones, x, y), $"zone at ({x}, {y})");
        }

        foreach (var (width, height, ratio) in new[] { (1000d, 1000d, 2d), (1000d, 400d, 2d), (800d, 600d, 0d), (0d, 10d, 1d) })
        {
            MotionZoneEditing.Fit(width, height, ratio).Should().Be(CoreEditing.Fit(width, height, ratio));
        }
    }

    [Fact]
    public void Zones_Turn_With_The_Picture_As_In_Core()
    {
        foreach (var (webFrom, coreFrom) in Rotations)
        {
            foreach (var (webTo, coreTo) in Rotations)
            {
                var from = new CameraRotation(webFrom);
                var to = new CameraRotation(webTo);
                MotionZoneDraft.QuarterTurns(from, to).Should().Be(CoreGeometry.QuarterTurns(coreFrom, coreTo));

                var web = MotionZoneDraft.Turn([WebZone(Shape)], from, to);
                var core = CoreGeometry.Turn([CoreZoneOf(Shape)], coreFrom, coreTo);
                Pairs(web[0].Points).Should().Equal(Pairs(core[0].Points), $"{webFrom} to {webTo}");
            }
        }

        MotionZoneDraft.QuarterTurns(new CameraRotation("upsideDownAndSideways"), new CameraRotation(CameraRotation.Names.Rotate90))
            .Should().Be(1, "a rotation the client does not know counts as none");
    }

    [Fact]
    public void The_Draft_Tells_A_Change_As_In_Core()
    {
        var web = new MotionZoneDraft([WebZone(Shape)], new CameraRotation(CameraRotation.Names.None));
        var core = new CoreDraft([CoreZoneOf(Shape)], CoreRotation.None);

        foreach (var (webRotation, coreRotation) in Rotations.Reverse())
        {
            web.Turn(new CameraRotation(webRotation));
            core.Turn(coreRotation);
            Pairs(web.Zones[0].Points).Should().Equal(Pairs(core.Zones[0].Points));
            web.IsChanged.Should().Be(core.IsChanged);
        }

        web.Turn(new CameraRotation(CameraRotation.Names.Rotate90));
        core.Turn(CoreRotation.Rotate90);
        web.Change([WebZone([(0.1, 0.1), (0.3, 0.1), (0.3, 0.3)])]);
        core.Change([CoreZoneOf([(0.1, 0.1), (0.3, 0.1), (0.3, 0.3)])]);
        web.Turn(new CameraRotation(CameraRotation.Names.Rotate180));
        core.Turn(CoreRotation.Rotate180);

        Pairs(web.Zones[0].Points).Should().Equal(Pairs(core.Zones[0].Points));
        web.IsChanged.Should().Be(core.IsChanged).And.BeTrue();
    }

    [Fact]
    public void A_Zone_That_Differs_Only_In_Its_Own_Values_Is_A_Change_As_In_Core()
    {
        var zone = WebZone(Shape) with { Kind = new MotionZoneKind(MotionZoneKind.Names.Watch) };
        var web = new MotionZoneDraft([zone], new CameraRotation(CameraRotation.Names.None));
        var core = new CoreDraft([CoreZoneOf(Shape)], CoreRotation.None);
        var coreEdited = core.Zones[0].Clone();
        coreEdited.Sensitivity = 90;

        web.Change([zone with { Sensitivity = 90 }]);
        core.Change([coreEdited]);

        web.IsChanged.Should().Be(core.IsChanged).And.BeTrue();
        MotionZoneDraft.ValueEquals(zone, zone with { SmallestObjectPercent = 0.1 }).Should().BeFalse();
        web.Zones[0].Sensitivity.Should().Be(90);
    }

    private static List<ZonePoint> Web(
        IEnumerable<(double X, double Y)> corners)
        => [.. corners.Select(c => new ZonePoint(c.X, c.Y))];

    private static List<CoreZonePoint> Core(
        IEnumerable<(double X, double Y)> corners)
        => [.. corners.Select(c => new CoreZonePoint(c.X, c.Y))];

    private static MotionZone WebZone(IEnumerable<(double X, double Y)> corners)
        => new("Tree", new MotionZoneKind(MotionZoneKind.Names.Ignore), Web(corners), Color: null);

    private static CoreZone CoreZoneOf(
        IEnumerable<(double X, double Y)> corners)
        => new() { Name = "Tree", Points = Core(corners) };

    private static IEnumerable<(double X, double Y)> Pairs(
        IEnumerable<ZonePoint> corners)
        => corners.Select(c => (c.X, c.Y));

    private static IEnumerable<(double X, double Y)> Pairs(
        IEnumerable<CoreZonePoint> corners)
        => corners.Select(c => (c.X, c.Y));
}