namespace AtcSoft.VideoEngine.Capture;

/// <summary>
/// A recording read as grey pictures for analysis: as many a second as asked for, in order, at the
/// size asked for, turned the way the picture is displayed. Needs the FFmpeg libraries and its
/// command-line tool, which makes the test recording; skipped without them.
/// </summary>
public sealed class GreyFrameFileReaderTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "atcsoft-grey-" + Guid.NewGuid().ToString("N"));

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
    public void A_Recording_Is_Read_At_The_Rate_Asked_For_In_Order_At_The_Size_Asked_For()
    {
        var video = MakeRecording();

        using var reader = GreyFrameFileReader.Open(video, 80, 60);
        var frames = reader.ReadFrames(4, TestContext.Current.CancellationToken).ToList();

        reader.Duration.TotalSeconds.Should().BeApproximately(4, 0.2);
        frames.Should().HaveCount(16, "four a second for four seconds, on a fixed grid");
        frames.Select(f => f.Position).Should().BeInAscendingOrder();
        frames.Zip(frames.Skip(1), (a, b) => (b.Position - a.Position).TotalSeconds).Should().OnlyContain(gap => gap >= 0.2 && gap <= 0.3, "a quarter of a second, to the nearest picture");
        frames.Should().OnlyContain(f => f.Width == 80 && f.Height == 60 && f.Pixels.Length == 80 * 60);

        // Dark on the left, white on the right.
        var first = frames[0];
        first.Pixels[(30 * 80) + 10].Should().BeLessThan(40);
        first.Pixels[(30 * 80) + 70].Should().BeGreaterThan(215);
    }

    [Fact]
    public void A_Quarter_Turned_Recording_Is_Read_Upright()
    {
        var video = MakeRecording();

        // Turned a quarter clockwise, the white right half is at the bottom, and the picture is portrait.
        using var reader = GreyFrameFileReader.Open(video, 60, 80, VideoRotation.Rotate90);
        var first = reader.ReadFrames(1, TestContext.Current.CancellationToken).First();

        (first.Width, first.Height).Should().Be((60, 80));
        first.Pixels[(10 * 60) + 30].Should().BeLessThan(40);
        first.Pixels[(70 * 60) + 30].Should().BeGreaterThan(215);
    }

    [Fact]
    public void A_File_That_Is_Not_A_Recording_Cannot_Be_Opened()
    {
        TestEngine.Require();
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "not-a-video.mkv");
        File.WriteAllText(path, "not a video");

        var open = () => GreyFrameFileReader.Open(path, 80, 60);

        open.Should().Throw<Exception>();
    }

    // Four seconds at 25 a second, 160 x 120: dark on the left half, white on the right.
    private string MakeRecording()
    {
        TestEngine.Require();
        var folder = VideoEngineBootstrap.FindFFmpegPath();
        var tool = folder is null ? null : Path.Combine(folder, OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");
        Assert.SkipUnless(tool is not null && File.Exists(tool), "The FFmpeg command-line tool is not installed.");

        Directory.CreateDirectory(directory);
        var video = Path.Combine(directory, "left-dark-right-white.mkv");
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = tool,
            Arguments =
                "-hide_banner -loglevel error -f lavfi -i color=c=black:s=160x120:r=25:d=4 " +
                "-vf \"drawbox=x=80:y=0:w=80:h=120:color=white:t=fill\" -c:v mpeg4 -q:v 2 " +
                $"-y \"{video}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
        }) ?? throw new InvalidOperationException("FFmpeg could not be started.");
        process.WaitForExit(30_000).Should().BeTrue();
        process.ExitCode.Should().Be(0);
        return video;
    }
}