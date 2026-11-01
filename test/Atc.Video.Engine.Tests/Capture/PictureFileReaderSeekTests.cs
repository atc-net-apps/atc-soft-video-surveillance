namespace Atc.Video.Engine.Capture;

/// <summary>
/// The preview pictures of a recording are a few spread over it. The reader jumps to the key frame
/// before each one instead of decoding every picture on the way (an hour of decoding per hourly
/// file), and still lands where it was asked to.
/// </summary>
public sealed class PictureFileReaderSeekTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "atcsoft-picture-seek-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Pictures_Spread_Over_A_Recording_Are_Read_Where_They_Were_Asked_For()
    {
        var video = MakeRecording();
        using var reader = PictureFileReader.Open(video, 160, 120, decoderThreads: 1);

        var positions = new List<double>();
        for (var second = 0; second < 60; second += 10)
        {
            positions.Add(reader.ReadNext(TimeSpan.FromSeconds(second))!.Position.TotalSeconds);
        }

        positions.Should().HaveCount(6);
        positions.Select((position, i) => position - (i * 10)).Should().AllSatisfy(late => late.Should().BeInRange(0, 0.1));
    }

    // A minute at 25 pictures a second with a key frame every second.
    private string MakeRecording()
    {
        TestEngine.Require();
        var folder = VideoEngineBootstrap.FindFFmpegPath();
        var tool = folder is null ? null : Path.Combine(folder, OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");
        Assert.SkipUnless(tool is not null && File.Exists(tool), "The FFmpeg command-line tool is not installed.");

        Directory.CreateDirectory(directory);
        var video = Path.Combine(directory, "minute.mkv");
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = tool,
            Arguments =
                "-hide_banner -loglevel error -f lavfi -i testsrc=size=160x120:rate=25:duration=60 " +
                "-c:v mpeg4 -q:v 5 -g 25 " +
                $"-y \"{video}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
        }) ?? throw new InvalidOperationException("FFmpeg could not be started.");
        process.WaitForExit(60_000).Should().BeTrue();
        process.ExitCode.Should().Be(0);
        return video;
    }
}