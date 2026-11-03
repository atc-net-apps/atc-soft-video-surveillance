namespace AtcSoft.Video.Surveillance.Wpf.Core.Windows;

using BoundingBoxSettings = AtcSoft.Video.Surveillance.Models.Settings.BoundingBoxSettings;
using RecordingBoxLoader = AtcSoft.Video.Surveillance.Wpf.Core.Services.RecordingBoxLoader;

/// <summary>
/// The recording player shows its recording's motion boxes when it can find them (CameraWall), in
/// the camera's colour, and a toggle turns them on and off; a player that cannot find them offers
/// no toggle.
/// </summary>
public sealed class FullScreenRecordingBoxesTests
{
    [Fact]
    public void A_Player_That_Cannot_Find_Boxes_Offers_No_Toggle()
    {
        var offered = true;

        var error = WpfTestThread.Run(() =>
        {
            using var viewModel = Player();
            offered = viewModel.CanShowMotionBoxes;
        });

        error.Should().BeNull();
        offered.Should().BeFalse();
    }

    [Fact]
    public void Boxes_Are_Drawn_In_The_Cameras_Colour_And_Start_As_It_Shows_Them()
    {
        (bool Offered, bool Shown, string Color, int Thickness) seen = default;

        var error = WpfTestThread.Run(() =>
        {
            using var viewModel = Player();
            viewModel.UseMotionBoxes(NoBoxes, new BoundingBoxSettings { Color = "YellowGreen", Thickness = 3, ShowInFullScreen = false });
            seen = (viewModel.CanShowMotionBoxes, viewModel.ShowMotionBoxes, viewModel.BoxColor, viewModel.BoxThickness);
        });

        error.Should().BeNull();
        seen.Should().Be((true, false, "YellowGreen", 3));
    }

    [Fact]
    public void The_Toggle_Turns_The_Boxes_On_And_Off()
    {
        var states = new List<bool>();

        var error = WpfTestThread.Run(() =>
        {
            using var viewModel = Player();
            viewModel.UseMotionBoxes(NoBoxes);
            states.Add(viewModel.ShowMotionBoxes);
            viewModel.ToggleMotionBoxesCommand.Execute(null);
            states.Add(viewModel.ShowMotionBoxes);
            viewModel.ToggleMotionBoxesCommand.Execute(null);
            states.Add(viewModel.ShowMotionBoxes);
        });

        error.Should().BeNull();
        states.Should().Equal(true, false, true);
    }

    private static RecordingBoxLoader NoBoxes
        => _ => Task.FromResult<AtcSoft.Video.Surveillance.Helpers.Activity.RecordingBoxes?>(null);

    private static FullScreenRecordingWindowViewModel Player()
        => new(
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "no-such-recording.mkv"),
            "Cam_20261012_101500_000.mkv");
}