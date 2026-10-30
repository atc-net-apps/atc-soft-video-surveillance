namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// The web zone editor: picking, dragging, corners added and removed, and what the form sends.
/// </summary>
public sealed class MotionZoneEditorStateTests
{
    // The picture on the page is 1000 x 500: a fraction of 0.006 across is 6 pixels.
    private const double Width = 1000;
    private const double Height = 500;

    [Fact]
    public void A_Zone_Is_Picked_By_Its_Inside_And_Dragged_Without_Leaving_The_Picture()
    {
        var given = Square("Tree");
        var state = new MotionZoneEditorState([given]);

        state.PointerDown(0.4, 0.4, Width, Height);
        state.PointerMove(0.9, 0.4);

        state.SelectedZone.Should().Be(0);
        state.Corners(0).Max(c => c.X).Should().BeApproximately(1, 1e-9, "the zone stops at the right edge");
        state.Zones[0].Should().BeSameAs(given, "nothing changes until the pointer is let go");

        state.PointerUp().Should().BeTrue();
        state.Zones[0].Points.Min(c => c.X).Should().BeApproximately(0.6, 1e-9);
        given.Points[0].Should().Be(new ZonePoint(0.2, 0.2), "the zones given are left as they were");
    }

    [Fact]
    public void A_Corner_Of_The_Picked_Zone_Is_Dragged_By_Its_Handle()
    {
        var state = new MotionZoneEditorState([Square("Tree")]);
        state.PointerDown(0.4, 0.4, Width, Height);
        state.PointerUp().Should().BeFalse("a click without a move changes nothing");

        state.PointerDown(0.604, 0.2, Width, Height);
        state.PointerMove(0.7, 0.1);

        state.SelectedCorner.Should().Be(1);
        state.PointerUp().Should().BeTrue();
        state.Zones[0].Points[1].Should().Be(new ZonePoint(0.7, 0.1));
    }

    [Fact]
    public void A_Press_On_The_Edge_Just_Outside_Keeps_The_Zone_Picked_For_A_Double_Click()
    {
        var state = new MotionZoneEditorState([Square("Tree")]);
        state.PointerDown(0.4, 0.4, Width, Height);
        state.PointerUp();

        // 4 pixels above the top edge: outside the zone, within the grab distance.
        state.PointerDown(0.4, 0.192, Width, Height);
        state.PointerUp();

        state.SelectedZone.Should().Be(0);
        state.DoubleClick(0.4, 0.192, Width, Height).Should().BeTrue();
        state.Zones[0].Points.Should().HaveCount(5);
    }

    [Fact]
    public void A_Click_Outside_Every_Zone_Picks_None()
    {
        var state = new MotionZoneEditorState([Square("Tree")]);
        state.PointerDown(0.4, 0.4, Width, Height);

        state.PointerDown(0.9, 0.9, Width, Height);

        state.SelectedZone.Should().Be(-1);
        state.IsDragging.Should().BeFalse();
        state.PointerUp().Should().BeFalse();
    }

    [Fact]
    public void A_Double_Click_Adds_A_Corner_On_An_Edge_And_Removes_One_On_A_Corner()
    {
        var state = new MotionZoneEditorState([Square("Tree")]);
        state.PointerDown(0.4, 0.4, Width, Height);
        state.PointerUp();

        state.DoubleClick(0.45, 0.205, Width, Height).Should().BeTrue();
        state.Zones[0].Points.Should().HaveCount(5);
        state.Zones[0].Points[1].Should().Be(new ZonePoint(0.45, 0.205), "the corner is where the pointer is");
        state.SelectedCorner.Should().Be(1);

        state.DoubleClick(0.45, 0.205, Width, Height).Should().BeTrue();
        state.Zones[0].Points.Should().HaveCount(4);
        state.SelectedCorner.Should().Be(-1);
    }

    [Fact]
    public void The_Buttons_Add_And_Remove_Zones_And_Corners_Within_The_Limits()
    {
        var state = new MotionZoneEditorState([Square("Zone 1")]);
        state.CanRemoveCorner.Should().BeFalse("nothing is picked");

        state.AddZone(n => $"Zone {n}").Should().BeTrue();
        state.Zones.Select(z => z.Name).Should().Equal("Zone 1", "Zone 2");
        state.SelectedZone.Should().Be(1);
        state.Zones[1].Kind.Should().Be(new MotionZoneKind(MotionZoneKind.Names.Ignore));

        state.AddCorner(Width, Height).Should().BeTrue();
        state.Zones[1].Points.Should().HaveCount(5);
        state.SelectedCorner.Should().Be(1, "the middle of the top edge, the longest on a wide picture");

        state.RemoveCorner().Should().BeTrue();
        state.Zones[1].Points.Should().HaveCount(4);
        state.CanRemoveCorner.Should().BeFalse("no corner is picked any more");

        state.Rename(new string('x', 70)).Should().BeTrue();
        state.Zones[1].Name.Should().HaveLength(64, "the server takes no longer name");

        state.RemoveZone().Should().BeTrue();
        state.Zones.Should().ContainSingle();
        state.Picked.Should().BeNull();
    }

    [Fact]
    public void A_Picked_Zone_Becomes_A_Watch_Zone_With_Its_Own_Values_And_An_Ignore_Zone_Again_Without()
    {
        var state = new MotionZoneEditorState([Square("Tree")]);
        state.SetKind(new MotionZoneKind(MotionZoneKind.Names.Watch)).Should().BeFalse("nothing is picked");
        state.PointerDown(0.4, 0.4, Width, Height);
        state.PointerUp();

        state.SetKind(new MotionZoneKind(MotionZoneKind.Names.Watch)).Should().BeTrue();
        state.SetSensitivity(80).Should().BeTrue();
        state.SetSensitivity(80).Should().BeFalse("it is 80 already");
        state.SetSmallestObjectPercent(0.1).Should().BeTrue();
        (state.Zones[0].Sensitivity, state.Zones[0].SmallestObjectPercent).Should().Be((80, 0.1));

        state.SetColour("Lime").Should().BeTrue();
        state.Zones[0].Color.Should().Be("Lime");

        state.SetKind(new MotionZoneKind(MotionZoneKind.Names.Ignore)).Should().BeTrue();
        (state.Zones[0].Sensitivity, state.Zones[0].SmallestObjectPercent, state.Zones[0].Color).Should().Be(((int?)null, (double?)null, (string?)null), "an ignore zone has no values of its own");
    }

    [Fact]
    public void No_More_Than_Sixteen_Zones()
    {
        var state = new MotionZoneEditorState([.. Enumerable.Range(1, 16).Select(i => Square($"Zone {i}"))]);

        state.CanAddZone.Should().BeFalse();
        state.AddZone(n => $"Zone {n}").Should().BeFalse();
    }

    [Fact]
    public void Zones_From_The_Form_Keep_The_Pick_While_It_Is_There()
    {
        var state = new MotionZoneEditorState([Square("Tree"), Square("Gate")]);
        state.PointerDown(0.4, 0.4, Width, Height);
        state.SelectedZone.Should().Be(1, "the zone drawn last is on top");

        state.Load([Square("Tree"), Square("Gate")]);
        state.SelectedZone.Should().Be(1);

        state.Load([Square("Tree")]);
        state.SelectedZone.Should().Be(-1);
    }

    [Fact]
    public void The_Form_Sends_Zones_Of_A_New_Camera_And_Changed_Zones_Of_Another()
    {
        var none = new MotionZoneDraft([], new CameraRotation(CameraRotation.Names.None));
        MotionZoneDraft.ToSend(none, isNew: true).Should().BeNull();

        var stored = new MotionZoneDraft([Square("Tree")], new CameraRotation(CameraRotation.Names.None));
        stored.Turn(new CameraRotation(CameraRotation.Names.Rotate90));
        MotionZoneDraft.ToSend(stored, isNew: false).Should().BeNull("the server turns the stored zones itself");
        MotionZoneDraft.ToSend(stored, isNew: true).Should().ContainSingle();

        stored.Change([]);
        MotionZoneDraft.ToSend(stored, isNew: false).Should().NotBeNull().And.BeEmpty("all zones removed is a change");
    }

    [Fact]
    public void The_Drawing_Takes_The_Shape_Of_The_Turned_Snapshot()
    {
        MotionZoneShapes.AspectRatio((1920, 1080), quarters: 0).Should().BeApproximately(16d / 9, 1e-9);
        MotionZoneShapes.AspectRatio((1920, 1080), quarters: 1).Should().BeApproximately(9d / 16, 1e-9);
        MotionZoneShapes.AspectRatio(null, quarters: 1).Should().Be(MotionZoneEditing.DefaultAspectRatio);

        var width = MotionZoneShapes.Width(9d / 16);
        var (imageWidth, imageHeight, transform) = MotionZoneShapes.Picture(width, quarters: 1);
        (imageWidth, imageHeight).Should().Be((1000d, 562.5), "the snapshot keeps its own shape and is turned into the drawing");
        transform.Should().Be("translate(281.25 500) rotate(90) translate(-500 -281.25)");
    }

    [Fact]
    public void Zones_Are_Drawn_With_Points_In_Any_Language()
    {
        using var danish = new CultureScope("da-DK");

        MotionZoneShapes.Points([new ZonePoint(0.25, 0.5), new ZonePoint(0.5, 0.125)], 2000).Should().Be("500,500 1000,125");
        MotionZoneShapes.Handle(new ZonePoint(0.5, 0.5), 2000).Should().Be(new TimelineRect(992, 492, 16, 16));
        var watch = Square("Gate") with { Kind = new MotionZoneKind(MotionZoneKind.Names.Watch) };
        MotionZoneShapes.Colour(Square("Tree")).Should().Be("#E53935");
        MotionZoneShapes.Colour(watch).Should().Be("#1E88E5");
        MotionZoneShapes.Colour(watch with { Color = "Lime" }).Should().Be("#00FF00", "a watch zone in its own colour, as on the timeline");
        MotionZoneShapes.Colour(Square("Tree") with { Color = "Lime" }).Should().Be("#E53935", "an ignore zone has no colour of its own");
        MotionZoneShapes.Stroke(watch, picked: true).Should().Be("#FFFFFF");
    }

    [Fact]
    public void The_Size_Of_A_Snapshot_Is_Read_From_Its_Header()
    {
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52, 0, 0, 0x07, 0x80, 0, 0, 0x04, 0x38, 8, 6, 0, 0, 0];

        MotionZoneShapes.PngSize(png).Should().Be((1920, 1080));
        MotionZoneShapes.PngSize([1, 2, 3]).Should().BeNull();
        MotionZoneShapes.PngSize(null).Should().BeNull();
    }

    private static MotionZone Square(string name)
        => new(name, new MotionZoneKind(MotionZoneKind.Names.Ignore), [new(0.2, 0.2), new(0.6, 0.2), new(0.6, 0.6), new(0.2, 0.6)], Color: null);

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo previous = CultureInfo.CurrentCulture;

        public CultureScope(string name)
            => CultureInfo.CurrentCulture = new CultureInfo(name);

        public void Dispose()
            => CultureInfo.CurrentCulture = previous;
    }
}