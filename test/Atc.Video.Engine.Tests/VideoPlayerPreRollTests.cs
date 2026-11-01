namespace Atc.Video.Engine;

/// <summary>
/// A recording started from the player's kept packets begins before it was started: a motion
/// recording holds the seconds before the motion. The source is a live H.264 test stream from the
/// FFmpeg command-line tool (a key frame every second); a machine without it skips the test.
/// </summary>
public sealed class VideoPlayerPreRollTests : IDisposable
{
    private const int FramesPerSecond = 15;
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(20);

    private readonly string directory = Path.Combine(Path.GetTempPath(), "atcsoft-preroll-" + Guid.NewGuid().ToString("N"));

    public VideoPlayerPreRollTests()
    {
        Directory.CreateDirectory(directory);
    }

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
    public async Task A_Recording_Started_With_A_Pre_Roll_Holds_The_Seconds_Before_It_Was_Started()
    {
        TestEngine.Require();
        var tool = FindFFmpegTool();
        Assert.SkipWhen(tool is null, "The FFmpeg command-line tool is not installed.");

        var cancellationToken = TestContext.Current.CancellationToken;
        var port = GetFreePort();
        using var source = StartH264Stream(tool!, port);
        var path = Path.Combine(directory, "motion.mkv");
        DateTime? start;
        DateTime asked;

        using (var player = new VideoPlayer(Microsoft.Extensions.Logging.Abstractions.NullLogger<VideoPlayer>.Instance))
        {
            try
            {
                player.Open(
                    new Uri($"tcp://127.0.0.1:{port}"),
                    new StreamOptions { HardwareAcceleration = false, RecordingPreRoll = TimeSpan.FromSeconds(5) });
                await WaitUntilAsync(() => player.State == PlayerState.Playing && player.FramesDecoded > 0, cancellationToken);
                await Task.Delay(TimeSpan.FromSeconds(6), cancellationToken);

                // Act
                asked = DateTime.UtcNow;
                start = player.FindRecordingStart(TimeSpan.FromSeconds(3));
                player.StartRecording(path, start!.Value);
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                player.StopRecording();
            }
            finally
            {
                player.Close();
                Stop(source);
            }
        }

        // Assert — the key frame at least 3 s back (one a second), then a second after the start
        start.Should().NotBeNull();
        (asked - start!.Value).Should().BeGreaterThanOrEqualTo(TimeSpan.FromSeconds(3)).And.BeLessThan(TimeSpan.FromSeconds(4.5));
        var recorded = ReadBack(path);
        recorded.FirstIsKeyFrame.Should().BeTrue();
        recorded.Frames.Should().BeGreaterThan(
            (int)(3.5 * FramesPerSecond),
            "the file holds the 3 s and more before the start, not only the second after it");
    }

    [Fact]
    public async Task Without_A_Pre_Roll_A_Recording_Starts_At_The_Last_Key_Frame()
    {
        TestEngine.Require();
        var tool = FindFFmpegTool();
        Assert.SkipWhen(tool is null, "The FFmpeg command-line tool is not installed.");

        var cancellationToken = TestContext.Current.CancellationToken;
        var port = GetFreePort();
        using var source = StartH264Stream(tool!, port);
        DateTime? start;
        DateTime asked;

        using (var player = new VideoPlayer(Microsoft.Extensions.Logging.Abstractions.NullLogger<VideoPlayer>.Instance))
        {
            try
            {
                player.Open(new Uri($"tcp://127.0.0.1:{port}"), new StreamOptions { HardwareAcceleration = false });
                await WaitUntilAsync(() => player.State == PlayerState.Playing && player.FramesDecoded > 0, cancellationToken);
                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);

                asked = DateTime.UtcNow;
                start = player.FindRecordingStart(TimeSpan.FromSeconds(3));
            }
            finally
            {
                player.Close();
                Stop(source);
            }
        }

        start.Should().NotBeNull();
        (asked - start!.Value).Should().BeLessThan(
            TimeSpan.FromSeconds(3.5),
            "a player kept for no pre-roll keeps the last key frame and a little more, not seconds of it");
    }

    [Fact]
    public async Task The_Next_File_Begins_At_The_Key_Frame_It_Is_Named_By()
    {
        // Before, the next file was named when the switch was asked for and began at the next key
        // frame: everything on the timeline was up to a group of pictures late in it.
        TestEngine.Require();
        var tool = FindFFmpegTool();
        Assert.SkipWhen(tool is null, "The FFmpeg command-line tool is not installed.");

        var cancellationToken = TestContext.Current.CancellationToken;
        var port = GetFreePort();
        using var source = StartH264Stream(tool!, port);
        var first = Path.Combine(directory, "first.mkv");
        var second = Path.Combine(directory, "second.mkv");
        DateTime start;
        DateTime stopped;

        using (var player = new VideoPlayer(Microsoft.Extensions.Logging.Abstractions.NullLogger<VideoPlayer>.Instance))
        {
            try
            {
                player.Open(
                    new Uri($"tcp://127.0.0.1:{port}"),
                    new StreamOptions { HardwareAcceleration = false, RecordingPreRoll = TimeSpan.FromSeconds(5) });
                await WaitUntilAsync(() => player.State == PlayerState.Playing && player.FramesDecoded > 0, cancellationToken);
                player.StartRecording(first);
                await Task.Delay(TimeSpan.FromSeconds(2.5), cancellationToken);

                // Act — half-way between two key frames
                start = player.FindRecordingStart(TimeSpan.Zero)!.Value;
                player.SwitchRecording(second, start);
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                stopped = DateTime.UtcNow;
                player.StopRecording();
            }
            finally
            {
                player.Close();
                Stop(source);
            }
        }

        // Assert — the second file holds every picture from its named start on
        var recorded = ReadBack(second);
        recorded.FirstIsKeyFrame.Should().BeTrue();
        var expected = (stopped - start).TotalSeconds * FramesPerSecond;
        recorded.Frames.Should().BeInRange(
            (int)(expected - (0.4 * FramesPerSecond)),
            (int)(expected + (0.4 * FramesPerSecond)),
            "second 0 of the file is the moment it is named by");
        ReadBack(first).Frames.Should().BeGreaterThan(FramesPerSecond, "the first file runs until the switch");
    }

    private static System.Diagnostics.Process StartH264Stream(
        string tool,
        int port)
    {
        var start = new System.Diagnostics.ProcessStartInfo
        {
            FileName = tool,
            Arguments =
                $"-hide_banner -loglevel error -re -f lavfi -i testsrc=size=320x240:rate={FramesPerSecond} " +
                $"-c:v libx264 -preset ultrafast -tune zerolatency -g {FramesPerSecond} -pix_fmt yuv420p -t 60 " +
                $"-f mpegts \"tcp://127.0.0.1:{port}?listen=1\"",
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        return System.Diagnostics.Process.Start(start)
            ?? throw new InvalidOperationException("The test stream could not be started.");
    }

    private static string? FindFFmpegTool()
    {
        var folder = VideoEngineBootstrap.FindFFmpegPath();
        if (folder is null)
        {
            return null;
        }

        var tool = Path.Combine(folder, OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");
        return File.Exists(tool) ? tool : null;
    }

    private static int GetFreePort()
    {
        using var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static void Stop(System.Diagnostics.Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(5000);
            }
        }
        catch (InvalidOperationException)
        {
            // Already gone.
        }
    }

    private static async Task WaitUntilAsync(
        Func<bool> condition,
        CancellationToken cancellationToken)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        while (!condition())
        {
            if (watch.Elapsed > StartTimeout)
            {
                Assert.Fail($"The player did not start playing within {StartTimeout.TotalSeconds:N0} s.");
            }

            await Task.Delay(100, cancellationToken);
        }
    }

    private static unsafe (int Frames, bool FirstIsKeyFrame) ReadBack(
        string path)
    {
        AVFormatContext* context = null;
        AVDictionary* options = null;
        avformat_open_input(ref context, path, null, ref options).Should().Be(0, $"'{path}' has to be a readable recording");
        var packet = av_packet_alloc();
        try
        {
            var frames = 0;
            var firstIsKeyFrame = false;
            while (av_read_frame(context, packet) >= 0)
            {
                if (frames == 0)
                {
                    firstIsKeyFrame = (packet->flags & PktFlags.Key) != PktFlags.None;
                }

                frames++;
                av_packet_unref(packet);
            }

            return (frames, firstIsKeyFrame);
        }
        finally
        {
            av_packet_free(ref packet);
            avformat_close_input(ref context);
        }
    }
}