namespace AtcSoft.VideoEngine.Windows.Previews;

using ActivityLogWriter = AtcSoft.VideoSurveillance.Helpers.Activity.ActivityLogWriter;
using BoundingBox = AtcSoft.VideoSurveillance.Models.BoundingBox;
using Drawing = System.Drawing;
using MotionDetectorResult = AtcSoft.VideoSurveillance.Models.MotionDetectorResult;
using MotionHoldChange = AtcSoft.VideoSurveillance.Enums.MotionHoldChange;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;
using RecordingPathHelper = AtcSoft.VideoSurveillance.Helpers.RecordingPathHelper;
using RecordingPreviewEvent = AtcSoft.VideoSurveillance.Models.RecordingPreviewEvent;
using RecordingPreviewFile = AtcSoft.VideoSurveillance.Helpers.Activity.RecordingPreviewFile;

/// <summary>
/// The previews of a recording file: a tile a second for a short recording, an extra tile of the
/// busiest second of each event in its activity index, upright and in colour, in one JPEG beside
/// the video with its index. Needs the FFmpeg libraries and its command-line tool, which makes the
/// test recording; skipped without them.
/// </summary>
public sealed class RecordingPreviewBuilderTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "atcsoft-previews-" + Guid.NewGuid().ToString("N"));

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

        var previews = new RecordingPreviewBuilder().Build(video, TestContext.Current.CancellationToken)!;

        previews.Should().NotBeNull();
        (previews.TileWidth, previews.TileHeight).Should().Be((160, 120), "the longest side 160, the shape of 320 x 240");
        previews.IntervalSeconds.Should().Be(1);
        previews.GridCount.Should().Be(11, "from 0 to 10 seconds, a second apart");
        previews.Events.Should().ContainSingle().Which.Should().Be(new RecordingPreviewEvent(6.5, 11), "the middle of the busy second, after the grid");
        RecordingPreviewFile.Read(video).Should().BeEquivalentTo(previews);

        using var sheet = new Drawing.Bitmap(RecordingPathHelper.BuildPreviewImagePath(video));
        (sheet.Width, sheet.Height).Should().Be((12 * 160, 120), "twelve tiles fit on one row");

        // Each tile is the picture: dark on the left, white on the right.
        foreach (var tile in new[] { 0, 5, 11 })
        {
            sheet.GetPixel((tile * 160) + 20, 60).R.Should().BeLessThan(60, "tile {0} is dark on the left", tile);
            sheet.GetPixel((tile * 160) + 140, 60).R.Should().BeGreaterThan(195, "tile {0} is white on the right", tile);
        }

        File.Exists(RecordingPathHelper.BuildPreviewImagePath(video) + ".tmp").Should().BeFalse("nothing half-written is left");
    }

    [Fact]
    public void A_Recording_Without_An_Index_Gets_The_Grid_Only()
    {
        var video = MakeRecording();

        var previews = new RecordingPreviewBuilder().Build(video, TestContext.Current.CancellationToken)!;

        previews.Events.Should().BeEmpty();
        previews.GridCount.Should().Be(11);
    }

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
        var folder = VideoEngineBootstrap.FindFFmpegPath();
        var tool = folder is null ? null : Path.Combine(folder, "ffmpeg.exe");
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