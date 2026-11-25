namespace Atc.Video.Engine;

/// <summary>
/// A stream held only to be recorded (a camera's main stream, while its sub-stream is shown and
/// analysed) is read but not decoded: it records, with the pre-roll, and feeds a live stream from
/// its packets, while nothing is decoded. The source is a live test stream from the FFmpeg
/// command-line tool; a machine without it skips the test. Nothing here asserts how long
/// anything took: the CI runners are too unsteady for that.
/// </summary>
public sealed class VideoPlayerUndecodedTests : IDisposable
{
    private const int FramesPerSecond = 15;
    private const string H264 = "-c:v libx264 -preset ultrafast -tune zerolatency -pix_fmt yuv420p";
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(20);

    private readonly string directory = Path.Combine(Path.GetTempPath(), "atcsoft-undecoded-" + Guid.NewGuid().ToString("N"));

    public VideoPlayerUndecodedTests()
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
    public async Task An_Undecoded_Stream_Records_With_Its_Pre_Roll_And_Decodes_Nothing()
    {
        TestEngine.Require();
        var tool = FindFFmpegTool();
        Assert.SkipWhen(tool is null, "The FFmpeg command-line tool is not installed.");

        var cancellationToken = TestContext.Current.CancellationToken;
        var port = GetFreePort();
        using var source = StartStream(tool!, port, H264);
        var path = Path.Combine(directory, "motion.mkv");
        DateTime? start;
        DateTime asked;
        long framesDecoded;
        VideoStreamInfo? info;
        byte[]? picture;
        Capture.GreyFrame? grey;

        using (var player = new VideoPlayer(Microsoft.Extensions.Logging.Abstractions.NullLogger<VideoPlayer>.Instance))
        {
            try
            {
                player.Open(
                    new Uri($"tcp://127.0.0.1:{port}"),
                    new StreamOptions { HardwareAcceleration = false, RecordingPreRoll = TimeSpan.FromSeconds(5), DecodeVideo = false });
                await WaitUntilAsync(() => player.State == PlayerState.Playing && player.FindRecordingStart(TimeSpan.Zero) is not null, cancellationToken);
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);

                info = player.StreamInfo;
                picture = await player.CaptureFrameAsync(cancellationToken);
                grey = await player.CaptureGreyAsync(80, 60, cancellationToken);

                // Act
                asked = DateTime.UtcNow;
                start = player.FindRecordingStart(TimeSpan.FromSeconds(3));
                player.StartRecording(path, start!.Value);
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                player.StopRecording();
                framesDecoded = player.FramesDecoded;
            }
            finally
            {
                player.Close();
                Stop(source);
            }
        }

        // Assert
        framesDecoded.Should().Be(0, "the stream is read, not decoded");
        picture.Should().BeNull("there is no decoded picture to capture");
        grey.Should().BeNull("there is no decoded picture to analyse");
        info.Should().NotBeNull();
        (info!.Width, info.Height, info.CodecName).Should().Be((320, 240, "h264"), "the size comes from the stream itself");
        (asked - start!.Value).Should().BeGreaterThanOrEqualTo(TimeSpan.FromSeconds(3), "the pre-roll is kept without decoding");
        var recorded = ReadBack(path);
        recorded.FirstIsKeyFrame.Should().BeTrue();
        recorded.Frames.Should().BeGreaterThan((int)(3.5 * FramesPerSecond), "the file holds the seconds before the start");
    }

    [Fact]
    public async Task An_Undecoded_Stream_Feeds_A_Live_Stream()
    {
        TestEngine.Require();
        var tool = FindFFmpegTool();
        Assert.SkipWhen(tool is null, "The FFmpeg command-line tool is not installed.");

        var cancellationToken = TestContext.Current.CancellationToken;
        var port = GetFreePort();
        using var source = StartStream(tool!, port, H264);
        var liveFolder = Path.Combine(directory, "live");
        Directory.CreateDirectory(liveFolder);
        int segments;

        using (var player = new VideoPlayer(Microsoft.Extensions.Logging.Abstractions.NullLogger<VideoPlayer>.Instance))
        {
            try
            {
                player.Open(new Uri($"tcp://127.0.0.1:{port}"), new StreamOptions { HardwareAcceleration = false, DecodeVideo = false });
                await WaitUntilAsync(() => player.State == PlayerState.Playing, cancellationToken);

                player.StartStreaming(Path.Combine(liveFolder, "stream.m3u8"));
                await WaitUntilAsync(() => Directory.GetFiles(liveFolder, "*.ts").Length >= 2, cancellationToken);
                segments = Directory.GetFiles(liveFolder, "*.ts").Length;
                player.StopStreaming();
            }
            finally
            {
                player.Close();
                Stop(source);
            }
        }

        segments.Should().BeGreaterThanOrEqualTo(2, "the packets go on to the live stream as they are");
    }

    [Fact]
    public async Task A_Stream_That_Has_To_Be_Encoded_To_Be_Recorded_Is_Decoded_Anyway()
    {
        // Recorded as H.264 while the source sends MPEG-4: the pictures are what goes into the file.
        TestEngine.Require();
        var tool = FindFFmpegTool();
        Assert.SkipWhen(tool is null, "The FFmpeg command-line tool is not installed.");

        var cancellationToken = TestContext.Current.CancellationToken;
        var port = GetFreePort();
        using var source = StartStream(tool!, port, "-c:v mpeg4 -q:v 5");
        long framesDecoded;

        using (var player = new VideoPlayer(Microsoft.Extensions.Logging.Abstractions.NullLogger<VideoPlayer>.Instance))
        {
            try
            {
                player.Open(
                    new Uri($"tcp://127.0.0.1:{port}"),
                    new StreamOptions { HardwareAcceleration = false, DecodeVideo = false, TranscodeRecordingToH264 = true });
                await WaitUntilAsync(() => player.State == PlayerState.Playing && player.FramesDecoded > 0, cancellationToken);
                framesDecoded = player.FramesDecoded;
            }
            finally
            {
                player.Close();
                Stop(source);
            }
        }

        framesDecoded.Should().BePositive();
    }

    private static System.Diagnostics.Process StartStream(
        string tool,
        int port,
        string codec)
    {
        var start = new System.Diagnostics.ProcessStartInfo
        {
            FileName = tool,
            Arguments =
                $"-hide_banner -loglevel error -re -f lavfi -i testsrc=size=320x240:rate={FramesPerSecond} " +
                $"{codec} -g {FramesPerSecond} -t 60 " +
                $"-f mpegts \"tcp://127.0.0.1:{port}?listen=1\"",
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        return TestStreams.Listening(
            System.Diagnostics.Process.Start(start) ?? throw new InvalidOperationException("The test stream could not be started."),
            port);
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
                Assert.Fail($"The condition was not met within {StartTimeout.TotalSeconds:N0} s.");
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