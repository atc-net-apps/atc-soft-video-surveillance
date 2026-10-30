namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

using CoreBoundingBox = AtcSoft.VideoSurveillance.Models.BoundingBox;
using CoreBoundingBoxSettings = AtcSoft.VideoSurveillance.Models.Settings.BoundingBoxSettings;
using CoreSmoother = AtcSoft.VideoSurveillance.Helpers.BoxSmoother;

/// <summary>
/// The web client smooths the motion boxes of a live tile like the desktop apps do, with its
/// own copy of Core's smoother. These tests check the copy follows each box by its track and
/// stays equal to the original.
/// </summary>
public sealed class MotionBoxSmootherTests
{
    [Fact]
    public void The_First_Boxes_Are_Shown_Where_They_Are()
    {
        var smoother = new MotionBoxSmoother();
        var box = new MotionBox(X: 10, Y: 20, Width: 30, Height: 40, TrackId: 1);

        smoother.Smooth([box]).Should().ContainSingle().Which.Should().Be(box);
    }

    [Fact]
    public void A_Box_Moves_Part_Of_The_Way_Towards_Where_Its_Track_Is_Now()
    {
        var smoother = new MotionBoxSmoother(0.5);
        smoother.Smooth([new MotionBox(X: 0, Y: 0, Width: 10, Height: 10, TrackId: 1)]);

        var moved = smoother.Smooth([new MotionBox(X: 20, Y: 0, Width: 10, Height: 10, TrackId: 1)]);

        moved.Should().ContainSingle().Which.X.Should().Be(10);
    }

    [Fact]
    public void A_Box_Follows_Its_Own_Track_Even_When_Another_Box_Is_Nearer()
    {
        var smoother = new MotionBoxSmoother(0.5);
        smoother.Smooth(
        [
            new MotionBox(X: 0, Y: 0, Width: 10, Height: 10, TrackId: 1),
            new MotionBox(X: 100, Y: 0, Width: 10, Height: 10, TrackId: 2),
        ]);

        // Track 2 jumped next to where track 1 was; it keeps following its own old place.
        var boxes = smoother.Smooth([new MotionBox(X: 2, Y: 0, Width: 10, Height: 10, TrackId: 2)]);

        boxes.Should().ContainSingle().Which.X.Should().Be(51);
    }

    [Fact]
    public void After_A_Reset_The_Boxes_Are_Shown_Where_They_Are()
    {
        var smoother = new MotionBoxSmoother(0.5);
        smoother.Smooth([new MotionBox(X: 0, Y: 0, Width: 10, Height: 10, TrackId: 1)]);
        smoother.Reset();

        smoother.Smooth([new MotionBox(X: 20, Y: 0, Width: 10, Height: 10, TrackId: 1)])
            .Should().ContainSingle().Which.X.Should().Be(20);
    }

    [Fact]
    public void The_Default_Is_The_Default_Of_The_Desktop_Apps()
        => MotionBoxSmoother.DefaultSmoothingFactor.Should().Be(new CoreBoundingBoxSettings().Smoothing);

    [Fact]
    public void The_Copy_Smooths_Like_The_Desktop_Apps()
    {
        (double X, double Y, double W, double H, int? Id)[][] updates =
        [
            [(0, 0, 20, 20, 1), (100, 50, 30, 30, 2)],
            [(110, 52, 32, 30, 2), (6, 1, 20, 22, 1)],
            [(12, 2, 20, 22, 1), (200, 200, 10, 10, 3)],
            [(15, 3, 21, 22, null), (205, 201, 10, 12, null)],
            [(18, 4, 21, 22, null), (60, 60, 8, 8, null), (207, 203, 10, 12, null)],
            [],
            [(1, 1, 5, 5, 4)],
        ];

        var web = new MotionBoxSmoother();
        var core = new CoreSmoother(new CoreBoundingBoxSettings().Smoothing);

        foreach (var update in updates)
        {
            var fromWeb = web.Smooth([.. update.Select(b => new MotionBox(X: b.X, Y: b.Y, Width: b.W, Height: b.H, TrackId: b.Id))]);
            var fromCore = core.Smooth([.. update.Select(b => new CoreBoundingBox { X = b.X, Y = b.Y, Width = b.W, Height = b.H, TrackId = b.Id })]);

            fromWeb.Select(b => (b.X, b.Y, b.Width, b.Height, b.TrackId))
                .Should().Equal(fromCore.Select(b => (b.X, b.Y, b.Width, b.Height, b.TrackId)));
        }
    }
}