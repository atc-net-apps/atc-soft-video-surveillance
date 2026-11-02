namespace AtcSoft.Video.Surveillance.Api.Services.Pictures;

/// <summary>
/// Recording thumbnails where GDI+ is not there: one frame or four in a grid, black where a frame
/// is missing, a portrait frame fitted into its cell; written from the pictures the camera gives.
/// </summary>
public sealed class PortableThumbnailGeneratorTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "atcsoft-portable-thumbnails-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            Directory.Delete(directory, recursive: true);
        }
        catch (IOException)
        {
            // Best effort: a temp folder left behind is harmless.
        }
    }

    [Fact]
    public void A_Landscape_Frame_Fills_Its_Cell()
        => PortableThumbnailGenerator.FitIntoCell(1920, 1080).Should().Be((320, 240));

    [Fact]
    public void A_Portrait_Frame_Is_Fitted_Into_Its_Cell()
        => PortableThumbnailGenerator.FitIntoCell(1080, 1920).Should().Be((135, 240));

    [Fact]
    public void Four_Frames_Make_A_Grid_Black_Where_One_Is_Missing()
    {
        TestEngine.Require();
        PictureFrame[] frames = [Plain(320, 240, 255), Plain(320, 240, 128), Plain(135, 240, 200)];

        var png = PortableThumbnailGenerator.Draw(frames, tileCount: 4)!;

        var picture = PictureCodec.Decode(png, (width, height) => (width, height))!;
        (picture.Width, picture.Height).Should().Be((640, 480));
        Grey(picture, 160, 120).Should().Be(255, "the first frame is top left");
        Grey(picture, 480, 120).Should().Be(128, "the second is top right");
        Grey(picture, 160, 360).Should().Be(200, "the third, portrait, is centred bottom left");
        Grey(picture, 10, 360).Should().Be(0, "beside a portrait frame is black");
        Grey(picture, 480, 360).Should().Be(0, "the missing fourth is black");
    }

    [Fact]
    public async Task A_Recording_Gets_A_Thumbnail_Of_The_Camera_Pictures()
    {
        TestEngine.Require();
        Directory.CreateDirectory(directory);
        var video = Path.Combine(directory, "Garage_20261102_090000_000.mp4");
        await File.WriteAllBytesAsync(video, [0], TestContext.Current.CancellationToken);
        var picture = Plain(640, 480, 90);
        var pipeline = Substitute.For<IMediaPipeline>();
        pipeline.CaptureFrameAsync(Arg.Any<CancellationToken>())
            .Returns(PictureCodec.EncodePng(picture.Pixels, picture.Width, picture.Height));
        var sut = new PortableThumbnailGenerator(NullLogger<PortableThumbnailGenerator>.Instance)
        {
            CaptureInterval = TimeSpan.FromMilliseconds(10),
        };

        sut.StartCapture(Guid.NewGuid(), "Garage", pipeline, video, tileCount: 4);

        var thumbnail = Path.ChangeExtension(video, ".png");
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!File.Exists(thumbnail) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        var bytes = await File.ReadAllBytesAsync(thumbnail, TestContext.Current.CancellationToken);
        var written = PictureCodec.Decode(bytes, (width, height) => (width, height))!;
        (written.Width, written.Height).Should().Be((640, 480));
        Grey(written, 480, 360).Should().Be(90, "all four tiles are the camera's picture");
        File.Exists(thumbnail + ".tmp").Should().BeFalse("nothing half-written is left");
    }

    private static PictureFrame Plain(
        int width,
        int height,
        byte grey)
    {
        var pixels = new byte[width * height * 4];
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = grey;
            pixels[i + 1] = grey;
            pixels[i + 2] = grey;
            pixels[i + 3] = 255;
        }

        return new PictureFrame(TimeSpan.Zero, width, height, pixels);
    }

    private static byte Grey(
        PictureFrame picture,
        int x,
        int y)
        => picture.Pixels[((y * picture.Width) + x) * 4];
}