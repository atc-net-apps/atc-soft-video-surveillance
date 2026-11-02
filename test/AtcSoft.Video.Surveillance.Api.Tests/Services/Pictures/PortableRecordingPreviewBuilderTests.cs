namespace AtcSoft.Video.Surveillance.Api.Services.Pictures;

using ActivityLogWriter = AtcSoft.Video.Surveillance.Helpers.Activity.ActivityLogWriter;
using BoundingBox = AtcSoft.Video.Surveillance.Models.BoundingBox;
using MotionDetectorResult = AtcSoft.Video.Surveillance.Models.MotionDetectorResult;
using MotionHoldChange = AtcSoft.Video.Surveillance.Enums.MotionHoldChange;
using RecordingPreviewEvent = AtcSoft.Video.Surveillance.Models.RecordingPreviewEvent;
using RecordingPreviewFile = AtcSoft.Video.Surveillance.Helpers.Activity.RecordingPreviewFile;

/// <summary>
/// The previews of a recording where GDI+ is not there: the same sheet as on Windows, a tile a
/// second and one for each event of the activity index, in one JPEG beside the video. Needs the
/// FFmpeg libraries and its command-line tool, which makes the test recording; skipped without them.
/// </summary>
public sealed class PortableRecordingPreviewBuilderTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "atcsoft-portable-previews-" + Guid.NewGuid().ToString("N"));

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
    public void A_Recording_Gets_A_Tile_A_Second_And_One_For_Its_Event()
    {
        var video = MakeRecording();
        WriteActivity(video, movingSecond: 6);

        var previews = new PortableRecordingPreviewBuilder().Build(video, TestContext.Current.CancellationToken)!;

        previews.Should().NotBeNull();
        (previews.TileWidth, previews.TileHeight).Should().Be((160, 120));
        previews.GridCount.Should().Be(11);
        previews.Events.Should().ContainSingle().Which.Should().Be(new RecordingPreviewEvent(6.5, 11));
        RecordingPreviewFile.Read(video).Should().BeEquivalentTo(previews);

        var jpeg = File.ReadAllBytes(RecordingPathHelper.BuildPreviewImagePath(video));
        jpeg.Take(3).Should().Equal(0xFF, 0xD8, 0xFF);
        var sheet = PictureCodec.Decode(jpeg, (width, height) => (width, height))!;
        (sheet.Width, sheet.Height).Should().Be((12 * 160, 120), "twelve tiles fit on one row");

        // Each tile is the picture: dark on the left, white on the right.
        foreach (var tile in new[] { 0, 5, 11 })
        {
            Red(sheet, (tile * 160) + 20, 60).Should().BeLessThan(60, "tile {0} is dark on the left", tile);
            Red(sheet, (tile * 160) + 140, 60).Should().BeGreaterThan(195, "tile {0} is white on the right", tile);
        }
    }

    [Fact]
    public void A_Recording_Without_An_Index_Gets_The_Grid_Only()
    {
        var video = MakeRecording();

        var previews = new PortableRecordingPreviewBuilder().Build(video, TestContext.Current.CancellationToken)!;

        previews.Events.Should().BeEmpty();
        previews.GridCount.Should().Be(11);
    }

    private static byte Red(
        PictureFrame picture,
        int x,
        int y)
        => picture.Pixels[(((y * picture.Width) + x) * 4) + 2];

    private static void WriteActivity(
        string videoPath,
        int movingSecond)
    {
        var start = new DateTimeOffset(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);
        using var index = ActivityLogWriter.Create(RecordingPathHelper.BuildActivityPath(videoPath), start);
        for (var second = 0; second < 10; second++)
        {
            var moving = second == movingSecond;
            index.Add(
                start.AddSeconds(second),
                new MotionDetectorResult(
                    moving,
                    moving ? MotionHoldChange.Continues : MotionHoldChange.None,
                    moving ? 20 : 1,
                    moving ? [new BoundingBox { X = 10, Y = 10, Width = 40, Height = 80, TrackId = 1 }] : []));
        }
    }

    // Ten seconds at 25 a second, 320 x 240: dark on the left half, white on the right.
    private string MakeRecording()
    {
        TestEngine.Require();
        var folder = Atc.Video.Engine.VideoEngineBootstrap.FindFFmpegPath();
        var tool = folder is null ? null : Path.Combine(folder, OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");
        Assert.SkipUnless(tool is not null && File.Exists(tool), "The FFmpeg command-line tool is not installed.");

        Directory.CreateDirectory(directory);
        var video = Path.Combine(directory, "20261020_090000_000.mp4");
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = tool,
            Arguments =
                "-hide_banner -loglevel error -f lavfi -i color=c=black:s=320x240:r=25:d=10 " +
                "-vf \"drawbox=x=160:y=0:w=160:h=240:color=white:t=fill\" -c:v mpeg4 -q:v 2 " +
                $"-y \"{video}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
        }) ?? throw new InvalidOperationException("FFmpeg could not be started.");
        process.WaitForExit(30_000).Should().BeTrue();
        process.ExitCode.Should().Be(0);
        return video;
    }
}