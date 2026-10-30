namespace AtcSoft.VideoSurveillance.Wpf.Core.Dialogs;

using BitmapFrame = System.Windows.Media.Imaging.BitmapFrame;
using BitmapSource = System.Windows.Media.Imaging.BitmapSource;
using CameraConfiguration = AtcSoft.VideoSurveillance.Wpf.Core.Models.CameraConfiguration;
using ImageSource = System.Windows.Media.ImageSource;
using IWpfCoreSettingsService = AtcSoft.VideoSurveillance.Wpf.Core.Services.IApplicationSettingsService;
using MemoryStream = System.IO.MemoryStream;
using MotionZone = AtcSoft.VideoSurveillance.Models.MotionZone;
using PixelFormats = System.Windows.Media.PixelFormats;
using PngBitmapEncoder = System.Windows.Media.Imaging.PngBitmapEncoder;
using Translations = AtcSoft.VideoSurveillance.Resources.Translations;
using ZonePoint = AtcSoft.VideoSurveillance.Models.ZonePoint;

/// <summary>
/// The camera's motion zones in the camera dialog: drawn on a snapshot, turned with a rotation
/// chosen there, saved with Save and left alone by Cancel.
/// </summary>
public sealed class CameraConfigurationDialogViewModelZoneTests
{
    [Fact]
    public void A_Rotation_Chosen_In_The_Dialog_Turns_The_Zones_And_Save_Keeps_Them()
    {
        var camera = CameraWithZone();
        var vm = BuildVm(camera);

        vm.SelectedRotationKey = nameof(CameraRotation.Rotate90);

        vm.MotionZones[0].Points[0].X.Should().BeApproximately(0.8, 1e-9);
        vm.MotionZones[0].Points[0].Y.Should().BeApproximately(0.1, 1e-9);
        camera.MotionZones[0].Points[0].Should().Be(new ZonePoint(0.1, 0.2), "nothing is saved yet");

        vm.SaveCommand.Execute(null);

        camera.Display.Rotation.Should().Be(CameraRotation.Rotate90);
        camera.MotionZones[0].Points[0].X.Should().BeApproximately(0.8, 1e-9);
        camera.MotionZones[0].Points[0].Y.Should().BeApproximately(0.1, 1e-9);
    }

    [Fact]
    public void A_Zone_Without_Its_Own_Smallest_Object_Is_Shown_The_Cameras()
    {
        var vm = BuildVm(CameraWithZone());
        var changed = new List<string?>();
        vm.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        vm.CameraSmallestObjectPercent.Should().Be(new MotionDetectionSettings().SmallestObjectPercent, "the app's, as the camera has none of its own");

        vm.UseDefaultSmallestObjectPercent = false;
        vm.OverrideSmallestObjectPercent = 0.5m;

        vm.CameraSmallestObjectPercent.Should().Be(0.5);
        changed.Should().Contain(nameof(vm.CameraSmallestObjectPercent));

        vm.UseDefaultSmallestObjectPercent = true;
        vm.CameraSmallestObjectPercent.Should().Be(new MotionDetectionSettings().SmallestObjectPercent);
    }

    [Fact]
    public void The_Empty_Field_Shows_The_Cameras_Value_Or_Says_It_Is_The_Cameras()
    {
        AtcSoft.VideoSurveillance.Wpf.Core.UserControls.MotionZoneEditor.SmallestObjectHint(0.5)
            .Should().Be(0.5.ToString(CultureInfo.CurrentCulture));
        AtcSoft.VideoSurveillance.Wpf.Core.UserControls.MotionZoneEditor.SmallestObjectHint(cameraPercent: null)
            .Should().Be(Translations.CamerasOwn);
    }

    [Fact]
    public void Zones_Drawn_In_The_Editor_Are_A_Change_To_Save()
    {
        var camera = CameraWithZone();
        var vm = BuildVm(camera);
        vm.SaveCommand.CanExecute(null).Should().BeFalse("nothing changed yet");

        vm.MotionZones = [.. vm.MotionZones, Zone("Gate", 0.6)];

        vm.SaveCommand.CanExecute(null).Should().BeTrue();
        vm.SaveCommand.Execute(null);
        camera.MotionZones.Select(z => z.Name).Should().Equal("Tree", "Gate");
    }

    [Fact]
    public void Cancel_Leaves_The_Zones_As_They_Were()
    {
        var camera = CameraWithZone();
        var vm = BuildVm(camera);

        vm.MotionZones = [];
        vm.SelectedRotationKey = nameof(CameraRotation.Rotate180);
        vm.CancelCommand.Execute(null);

        camera.MotionZones.Should().ContainSingle().Which.Points[0].Should().Be(new ZonePoint(0.1, 0.2));
        camera.Display.Rotation.Should().Be(CameraRotation.None);
    }

    [Fact]
    public async Task The_Snapshot_Comes_From_The_Tile_And_Turns_With_The_Rotation_Chosen()
    {
        // The tile's picture is turned by the stored rotation already: 40 x 20 as shown at a quarter turn.
        var camera = CameraWithZone();
        camera.Display.Rotation = CameraRotation.Rotate90;
        var factory = Substitute.For<IVideoPlayerFactory>();
        var vm = BuildVm(camera, factory, livePicture: _ => Task.FromResult<byte[]?>(Png(40, 20)));
        vm.ZonePictureStatus.Should().NotBeNullOrEmpty("there is no picture yet");

        await vm.TakeZonePictureCommand.ExecuteAsync(null);

        factory.DidNotReceive().Create();
        vm.ZonePictureStatus.Should().BeNull();
        Size(vm.ZonePicture).Should().Be((40, 20));

        vm.SelectedRotationKey = nameof(CameraRotation.Rotate180);
        Size(vm.ZonePicture).Should().Be((20, 40), "a further quarter turn");

        vm.SelectedRotationKey = nameof(CameraRotation.Rotate90);
        Size(vm.ZonePicture).Should().Be((40, 20));
    }

    [Fact]
    public async Task Without_A_Tile_The_Camera_Is_Opened_For_A_Snapshot_Shown_At_The_Rotation_Chosen()
    {
        var player = Substitute.For<IVideoPlayer>();
        player
            .When(p => p.Open(Arg.Any<Uri>(), Arg.Any<StreamOptions>()))
            .Do(_ => player.StateChanged += Raise.EventWith(player, new PlayerStateChangedEventArgs(PlayerState.Stopped, PlayerState.Playing)));
        player.CaptureFrameAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<byte[]?>(null), Task.FromResult<byte[]?>(Png(40, 20)));
        var factory = Substitute.For<IVideoPlayerFactory>();
        factory.Create().Returns(player);
        var camera = CameraWithZone();
        camera.Display.Rotation = CameraRotation.Rotate90;
        var vm = BuildVm(camera, factory, livePicture: _ => Task.FromResult<byte[]?>(null));

        await vm.TakeZonePictureCommand.ExecuteAsync(null);

        Size(vm.ZonePicture).Should().Be((20, 40), "the player's picture is not turned, the camera is shown at a quarter turn");
        player.Received().Close();
    }

    [Fact]
    public async Task A_Camera_That_Does_Not_Play_Says_So()
    {
        var player = Substitute.For<IVideoPlayer>();
        player
            .When(p => p.Open(Arg.Any<Uri>(), Arg.Any<StreamOptions>()))
            .Do(_ => player.StateChanged += Raise.EventWith(player, new PlayerStateChangedEventArgs(PlayerState.Stopped, PlayerState.Error)));
        var factory = Substitute.For<IVideoPlayerFactory>();
        factory.Create().Returns(player);
        var vm = BuildVm(CameraWithZone(), factory);

        await vm.TakeZonePictureCommand.ExecuteAsync(null);

        vm.ZonePicture.Should().BeNull();
        vm.ZonePictureStatus.Should().Be(Translations.FailedToCaptureSnapshot);
    }

    private static CameraConfiguration CameraWithZone()
    {
        var camera = new CameraConfiguration
        {
            MotionZones = [Zone("Tree", 0.1)],
        };
        camera.Display.DisplayName = "Yard";
        camera.Connection.IpAddress = "192.0.2.30";
        return camera;
    }

    private static MotionZone Zone(
        string name,
        double x)
        => new()
        {
            Name = name,
            Points = [new ZonePoint(x, 0.2), new ZonePoint(x + 0.3, 0.2), new ZonePoint(x + 0.3, 0.6)],
        };

    private static byte[] Png(
        int width,
        int height)
    {
        var pixels = new byte[width * height * 4];
        var picture = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, palette: null, pixels, width * 4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(picture));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }

    private static (int Width, int Height) Size(ImageSource? picture)
        => picture is BitmapSource bitmap
            ? (bitmap.PixelWidth, bitmap.PixelHeight)
            : (0, 0);

    private static CameraConfigurationDialogViewModel BuildVm(
        CameraConfiguration camera,
        IVideoPlayerFactory? factory = null,
        Func<CancellationToken, Task<byte[]?>>? livePicture = null)
    {
        var settingsService = Substitute.For<IWpfCoreSettingsService>();
        settingsService.CameraDisplay.Returns(new CameraDisplayAppSettings());
        settingsService.Connection.Returns(new ConnectionAppSettings());
        settingsService.Recording.Returns(new RecordingSettings());
        settingsService.Performance.Returns(new PerformanceSettings());
        settingsService.MotionDetection.Returns(new MotionDetectionSettings());

        return new CameraConfigurationDialogViewModel(
            camera,
            isNew: false,
            [],
            settingsService,
            factory ?? Substitute.For<IVideoPlayerFactory>(),
            NullUsbCameraEnumerator.Instance,
            connectionTypeDetector: null,
            livePicture);
    }
}