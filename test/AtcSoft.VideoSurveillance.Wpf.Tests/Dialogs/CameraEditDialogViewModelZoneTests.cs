namespace AtcSoft.VideoSurveillance.Wpf.Dialogs;

using ApiMotionZone = global::VideoSurveillance.Generated.Cameras.Models.MotionZone;
using ApiMotionZoneKind = global::VideoSurveillance.Generated.Cameras.Models.MotionZoneKind;
using ApiZonePoint = global::VideoSurveillance.Generated.Cameras.Models.ZonePoint;
using BitmapFrame = System.Windows.Media.Imaging.BitmapFrame;
using BitmapSource = System.Windows.Media.Imaging.BitmapSource;
using CameraRotation = global::VideoSurveillance.Generated.Cameras.Models.CameraRotation;
using ImageSource = System.Windows.Media.ImageSource;
using PixelFormats = System.Windows.Media.PixelFormats;
using PngBitmapEncoder = System.Windows.Media.Imaging.PngBitmapEncoder;

/// <summary>
/// The motion zones in the client's camera dialog: drawn on the server's snapshot, turned with a
/// rotation chosen there, and sent only when they changed, so the server keeps (and turns) the
/// stored ones otherwise.
/// </summary>
public sealed class CameraEditDialogViewModelZoneTests
{
    [Fact]
    public void Zones_Left_Alone_Are_Left_Out_Even_When_The_Rotation_Changes()
    {
        var vm = new CameraEditDialogViewModel(CameraWithZone());

        vm.SelectedRotation = CameraRotation.Rotate90;

        vm.MotionZones[0].Points[0].X.Should().BeApproximately(0.8, 1e-9, "the editor shows the zone turned with the picture");
        vm.BuildUpdateRequest().MotionZones.Should().BeNull("the server turns the stored zones itself");
    }

    [Fact]
    public void Zones_Drawn_In_The_Editor_Are_Sent_As_Drawn()
    {
        var vm = new CameraEditDialogViewModel(CameraWithZone());
        vm.SelectedRotation = CameraRotation.Rotate180;
        var moved = vm.MotionZones[0].Clone();
        moved.Points[0] = new ZonePoint(0.5, 0.5);

        vm.MotionZones = [moved];

        var sent = vm.BuildUpdateRequest().MotionZones;
        sent.Should().ContainSingle();
        sent![0].Name.Should().Be("Tree");
        sent[0].Kind.Should().Be(ApiMotionZoneKind.Ignore);
        sent[0].Points[0].Should().Be(new ApiZonePoint(0.5, 0.5));
        sent[0].Points[1].X.Should().BeApproximately(0.6, 1e-9, "the other corners as shown at the half turn");
    }

    [Fact]
    public void All_Zones_Removed_Is_An_Empty_List_Not_Nothing()
    {
        var vm = new CameraEditDialogViewModel(CameraWithZone());

        vm.MotionZones = [];

        vm.BuildUpdateRequest().MotionZones.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void A_New_Camera_Sends_Its_Zones_Or_None()
    {
        var vm = new CameraEditDialogViewModel();
        vm.BuildCreateRequest().MotionZones.Should().BeNull();

        vm.MotionZones = [new MotionZone { Name = "Gate", Points = [new(0.1, 0.1), new(0.2, 0.1), new(0.2, 0.2)] }];

        vm.BuildCreateRequest().MotionZones.Should().ContainSingle().Which.Name.Should().Be("Gate");
        vm.TakeZonePictureCommand.CanExecute(null).Should().BeFalse("the server has no camera to take a snapshot of yet");
    }

    [Fact]
    public async Task The_Servers_Snapshot_Is_Shown_And_Turns_With_The_Rotation_Chosen()
    {
        // The server's snapshot is turned by the stored rotation: 40 x 20 as shown at a quarter turn.
        Guid? asked = null;
        var camera = CameraWithZone(CameraRotation.Rotate90);
        var vm = new CameraEditDialogViewModel(
            camera,
            takeSnapshot: (id, _) =>
            {
                asked = id;
                return Task.FromResult<byte[]?>(Png(40, 20));
            });
        vm.ZonePictureStatus.Should().Be(Translations.MotionZonesNoPicture);

        await vm.TakeZonePictureCommand.ExecuteAsync(null);

        asked.Should().Be(camera.Id);
        vm.ZonePictureStatus.Should().BeNull();
        Size(vm.ZonePicture).Should().Be((40, 20));

        vm.SelectedRotation = CameraRotation.None;
        Size(vm.ZonePicture).Should().Be((20, 40), "turned back a quarter");
    }

    [Fact]
    public async Task A_Snapshot_The_Server_Cannot_Take_Says_So()
    {
        var vm = new CameraEditDialogViewModel(
            CameraWithZone(),
            takeSnapshot: (_, _) => Task.FromException<byte[]?>(new HttpRequestException("Camera not connected")));

        await vm.TakeZonePictureCommand.ExecuteAsync(null);

        vm.ZonePicture.Should().BeNull();
        vm.ZonePictureStatus.Should().Be(Translations.FailedToCaptureSnapshot);
    }

    private static global::VideoSurveillance.Generated.Cameras.Models.Camera CameraWithZone(
        CameraRotation? rotation = null)
        => TestCameras.Network(rotation) with
        {
            MotionZones =
            [
                new ApiMotionZone("Tree", ApiMotionZoneKind.Ignore, [new(0.1, 0.2), new(0.4, 0.2), new(0.4, 0.6)], Color: null),
            ],
        };

    private static byte[] Png(
        int width,
        int height)
    {
        var picture = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, palette: null, new byte[width * height * 4], width * 4);
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
}