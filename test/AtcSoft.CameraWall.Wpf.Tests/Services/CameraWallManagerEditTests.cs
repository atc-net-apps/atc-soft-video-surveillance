namespace AtcSoft.CameraWall.Wpf.Services;

using IMotionDetectionService = AtcSoft.VideoSurveillance.Services.IMotionDetectionService;
using IUsbCameraWatcher = AtcSoft.VideoSurveillance.Services.IUsbCameraWatcher;
using MotionZone = AtcSoft.VideoSurveillance.Models.MotionZone;

/// <summary>
/// Editing a camera: the dialog may take its snapshot from the running tile, and the zones it
/// saves reach the running analysis at once.
/// </summary>
public sealed class CameraWallManagerEditTests
{
    [Fact]
    public void Saved_Zones_Reach_The_Running_Analysis_And_The_Dialog_Gets_A_Way_To_The_Tiles_Picture()
    {
        var motion = Substitute.For<IMotionDetectionService>();
        var dialog = Substitute.For<IDialogService>();
        var storage = Substitute.For<ICameraStorageService>();
        storage.GetAllLayouts().Returns([]);
        storage.GetAllCameras().Returns([]);
        var camera = new CameraConfiguration();
        Func<CancellationToken, Task<byte[]?>>? livePicture = null;
        dialog
            .ShowCameraConfigurationDialog(camera, isNew: false, Arg.Any<IReadOnlyCollection<(string, string?)>>(), Arg.Any<Func<CancellationToken, Task<byte[]?>>?>())
            .Returns(call =>
            {
                livePicture = call.ArgAt<Func<CancellationToken, Task<byte[]?>>?>(3);
                camera.MotionZones = [new MotionZone { Name = "Tree", Points = [new(0.1, 0.1), new(0.3, 0.1), new(0.3, 0.3)] }];
                return camera;
            });

        using var manager = Build(storage, dialog, motion);
        manager.EditCamera(camera);

        livePicture.Should().NotBeNull();
        storage.Received(1).AddOrUpdateCamera(camera);
        motion.Received(1).UseZones(camera.Id, Arg.Is<IReadOnlyList<MotionZone>>(z => z.Single().Name == "Tree"));
    }

    [Fact]
    public void A_Cancelled_Edit_Leaves_The_Running_Analysis_Alone()
    {
        var motion = Substitute.For<IMotionDetectionService>();
        var dialog = Substitute.For<IDialogService>();
        var storage = Substitute.For<ICameraStorageService>();
        storage.GetAllLayouts().Returns([]);
        storage.GetAllCameras().Returns([]);
        var camera = new CameraConfiguration();
        dialog
            .ShowCameraConfigurationDialog(camera, isNew: false, Arg.Any<IReadOnlyCollection<(string, string?)>>(), Arg.Any<Func<CancellationToken, Task<byte[]?>>?>())
            .Returns((CameraConfiguration?)null);

        using var manager = Build(storage, dialog, motion);
        manager.EditCamera(camera);

        motion.DidNotReceiveWithAnyArgs().UseZones(Guid.Empty, default!);
    }

    private static CameraWallManager Build(
        ICameraStorageService storage,
        IDialogService dialog,
        IMotionDetectionService motion)
        => new(
            NullLogger<CameraWallManager>.Instance,
            storage,
            dialog,
            Substitute.For<IApplicationSettingsService>(),
            Substitute.For<IRecordingService>(),
            motion,
            Substitute.For<ITimelapseService>(),
            Substitute.For<AtcSoft.VideoSurveillance.Services.IGitHubReleaseService>(),
            Substitute.For<IToastNotificationService>(),
            Substitute.For<IVideoPlayerFactory>(),
            Substitute.For<IUsbCameraWatcher>());
}