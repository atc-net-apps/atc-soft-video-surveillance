namespace AtcSoft.VideoEngine;

/// <summary>
/// The whole way through the player with a source that delivers uncompressed
/// pictures, as a webcam does. The source is a live test stream from the
/// FFmpeg command-line tool; a machine without it skips the test.
/// </summary>
public sealed class VideoPlayerRawVideoRecordingTests : IDisposable
{
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(20);

    private readonly string directory = Path.Combine(Path.GetTempPath(), "atcsoft-player-" + Guid.NewGuid().ToString("N"));

    public VideoPlayerRawVideoRecordingTests()
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

    [Theory]
    [InlineData("mkv")]
    [InlineData("mp4")]
    public async Task A_Source_Of_Uncompressed_Pictures_Is_Recorded_As_H264_Across_A_File_Switch(
        string format)
    {
        TestEngine.Require();
        var tool = FindFFmpegTool();
        Assert.SkipWhen(tool is null, "The FFmpeg command-line tool is not installed.");

        var cancellationToken = TestContext.Current.CancellationToken;
        var port = GetFreePort();
        using var source = StartRawVideoStream(tool!, port);
        var first = Path.Combine(directory, "first." + format);
        var second = Path.Combine(directory, "second." + format);

        using (var player = new VideoPlayer(Microsoft.Extensions.Logging.Abstractions.NullLogger<VideoPlayer>.Instance))
        {
            try
            {
                // Arrange
                player.Open(new Uri($"tcp://127.0.0.1:{port}"), new StreamOptions { HardwareAcceleration = false });
                await WaitUntilAsync(() => player.State == PlayerState.Playing && player.FramesDecoded > 0, cancellationToken);
                player.StreamInfo!.CodecName.Should().Be("rawvideo", "the test is about a source that is not compressed");

                // Act
                player.StartRecording(first);
                player.IsRecording.Should().BeTrue();
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);

                player.SwitchRecording(second);
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);

                player.StopRecording();
                player.IsRecording.Should().BeFalse();
            }
            finally
            {
                player.Close();
                Stop(source);
            }
        }

        // Assert — about two seconds of 15 pictures a second in each file
        foreach (var path in new[] { first, second })
        {
            var recorded = ReadBack(path);
            recorded.Codec.Should().Be(AVCodecID.H264);
            recorded.Frames.Should().BeInRange(20, 40);
            recorded.FirstIsKeyFrame.Should().BeTrue();
            new FileInfo(path).Length.Should().BeLessThan(2_000_000, "four seconds of raw 320x240 pictures would be 9 MB");
        }
    }

    [Theory]
    [InlineData(true, 2)]
    [InlineData(false, 1)]
    public async Task A_Source_With_Sound_Is_Recorded_With_It_Only_When_Asked_For(
        bool recordAudio,
        int expectedStreams)
    {
        TestEngine.Require();
        var tool = FindFFmpegTool();
        Assert.SkipWhen(tool is null, "The FFmpeg command-line tool is not installed.");

        var cancellationToken = TestContext.Current.CancellationToken;
        var port = GetFreePort();
        using var source = StartRawVideoStream(tool!, port, withSound: true);
        var path = Path.Combine(directory, $"sound-{recordAudio}.mp4");

        using (var player = new VideoPlayer(Microsoft.Extensions.Logging.Abstractions.NullLogger<VideoPlayer>.Instance))
        {
            try
            {
                player.Open(
                    new Uri($"tcp://127.0.0.1:{port}"),
                    new StreamOptions { HardwareAcceleration = false, RecordAudio = recordAudio });
                await WaitUntilAsync(() => player.State == PlayerState.Playing && player.FramesDecoded > 0, cancellationToken);

                player.StartRecording(path);
                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
                player.StopRecording();
            }
            finally
            {
                player.Close();
                Stop(source);
            }
        }

        var streams = ReadStreams(path);
        streams.Should().HaveCount(expectedStreams);
        streams[0].Codec.Should().Be(AVCodecID.H264);
        if (recordAudio)
        {
            streams[1].Codec.Should().Be(AVCodecID.Aac);
            streams[1].Seconds.Should().BeInRange(2.3, 3.5, "the sound runs as long as the picture");
            streams[0].Seconds.Should().BeInRange(2.3, 3.5);
        }
    }

    [Fact]
    public async Task A_Source_Is_Streamed_To_Browsers_And_Recorded_At_The_Same_Time()
    {
        // One open camera serves both: a webcam can only be opened once.
        TestEngine.Require();
        var tool = FindFFmpegTool();
        Assert.SkipWhen(tool is null, "The FFmpeg command-line tool is not installed.");

        var cancellationToken = TestContext.Current.CancellationToken;
        var port = GetFreePort();
        using var source = StartRawVideoStream(tool!, port);
        var recording = Path.Combine(directory, "while-streaming.mkv");
        var liveFolder = Path.Combine(directory, "live");
        Directory.CreateDirectory(liveFolder);
        var playlist = Path.Combine(liveFolder, "stream.m3u8");

        using (var player = new VideoPlayer(Microsoft.Extensions.Logging.Abstractions.NullLogger<VideoPlayer>.Instance))
        {
            try
            {
                player.Open(new Uri($"tcp://127.0.0.1:{port}"), new StreamOptions { HardwareAcceleration = false });
                await WaitUntilAsync(() => player.State == PlayerState.Playing && player.FramesDecoded > 0, cancellationToken);

                // Act — the stream starts first, the recording joins and leaves
                player.StartStreaming(playlist);
                player.IsStreaming.Should().BeTrue();
                player.StartRecording(recording);
                await WaitUntilAsync(() => File.Exists(playlist) && Directory.GetFiles(liveFolder, "*.ts").Length >= 2, cancellationToken);
                player.StopRecording();

                // Assert — the stream goes on after the recording stopped
                var segmentsAtStop = Directory.GetFiles(liveFolder, "*.ts").Length;
                await WaitUntilAsync(() => Directory.GetFiles(liveFolder, "*.ts").Length > segmentsAtStop, cancellationToken);
                player.IsStreaming.Should().BeTrue();
                player.IsRecording.Should().BeFalse();

                player.StopStreaming();
                player.IsStreaming.Should().BeFalse();
            }
            finally
            {
                player.Close();
                Stop(source);
            }
        }

        var recorded = ReadBack(recording);
        recorded.Codec.Should().Be(AVCodecID.H264);
        recorded.Frames.Should().BeGreaterThan(20);
        recorded.FirstIsKeyFrame.Should().BeTrue();
        (await File.ReadAllTextAsync(playlist, cancellationToken)).Should().Contain("#EXT-X-ENDLIST", "a stream that was stopped says so");
    }

    [Fact]
    public async Task The_Stream_To_Browsers_Has_The_Sound_And_Keeps_It_When_A_Recording_Comes_And_Goes()
    {
        // The stream and the recording share the one sound encoder, as they
        // share the picture encoder. Whichever stops must leave it running
        // for the other.
        TestEngine.Require();
        var tool = FindFFmpegTool();
        Assert.SkipWhen(tool is null, "The FFmpeg command-line tool is not installed.");

        var cancellationToken = TestContext.Current.CancellationToken;
        var port = GetFreePort();
        using var source = StartRawVideoStream(tool!, port, withSound: true);
        var recording = Path.Combine(directory, "sound-while-streaming.mkv");
        var secondRecording = Path.Combine(directory, "sound-after-streaming.mkv");
        var liveFolder = Path.Combine(directory, "live-sound");
        Directory.CreateDirectory(liveFolder);
        var playlist = Path.Combine(liveFolder, "stream.m3u8");
        string duringRecording;
        string afterRecording;

        using (var player = new VideoPlayer(Microsoft.Extensions.Logging.Abstractions.NullLogger<VideoPlayer>.Instance))
        {
            try
            {
                player.Open(
                    new Uri($"tcp://127.0.0.1:{port}"),
                    new StreamOptions { HardwareAcceleration = false, RecordAudio = true });
                await WaitUntilAsync(() => player.State == PlayerState.Playing && player.FramesDecoded > 0, cancellationToken);

                // The stream starts alone, a recording joins and leaves.
                player.StartStreaming(playlist);
                player.StartRecording(recording);
                await WaitUntilAsync(() => FinishedSegments(liveFolder).Count >= 2, cancellationToken);
                duringRecording = Keep(FinishedSegments(liveFolder)[^1], "during.ts");
                player.StopRecording();

                var segmentsAtStop = FinishedSegments(liveFolder).Count;
                await WaitUntilAsync(() => FinishedSegments(liveFolder).Count >= segmentsAtStop + 2, cancellationToken);
                afterRecording = Keep(FinishedSegments(liveFolder)[^1], "after.ts");

                // The stream leaves while a second recording runs on.
                player.StartRecording(secondRecording);
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                player.StopStreaming();
                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
                player.StopRecording();
            }
            finally
            {
                player.Close();
                Stop(source);
            }
        }

        ReadStreams(duringRecording).Select(s => s.Codec).Should().Equal(AVCodecID.H264, AVCodecID.Aac);
        ReadStreams(afterRecording).Select(s => s.Codec).Should().Equal(
            [AVCodecID.H264, AVCodecID.Aac],
            "the recording that stopped must not take the sound with it");

        var first = ReadStreams(recording);
        first.Select(s => s.Codec).Should().Equal(AVCodecID.H264, AVCodecID.Aac);

        var second = ReadStreams(secondRecording);
        second.Select(s => s.Codec).Should().Equal(AVCodecID.H264, AVCodecID.Aac);
        second[1].Seconds.Should().BeInRange(
            second[0].Seconds - 0.7,
            second[0].Seconds + 0.7,
            "the stream that stopped must not take the sound with it");
    }

    [Fact]
    public async Task A_Stream_Of_A_Source_Whose_Sound_Was_Not_Asked_For_Has_The_Picture_Only()
    {
        TestEngine.Require();
        var tool = FindFFmpegTool();
        Assert.SkipWhen(tool is null, "The FFmpeg command-line tool is not installed.");

        var cancellationToken = TestContext.Current.CancellationToken;
        var port = GetFreePort();
        using var source = StartRawVideoStream(tool!, port, withSound: true);
        var liveFolder = Path.Combine(directory, "live-silent");
        Directory.CreateDirectory(liveFolder);
        string segment;

        using (var player = new VideoPlayer(Microsoft.Extensions.Logging.Abstractions.NullLogger<VideoPlayer>.Instance))
        {
            try
            {
                player.Open(new Uri($"tcp://127.0.0.1:{port}"), new StreamOptions { HardwareAcceleration = false });
                await WaitUntilAsync(() => player.State == PlayerState.Playing && player.FramesDecoded > 0, cancellationToken);

                player.StartStreaming(Path.Combine(liveFolder, "stream.m3u8"));
                await WaitUntilAsync(() => FinishedSegments(liveFolder).Count >= 1, cancellationToken);
                segment = Keep(FinishedSegments(liveFolder)[^1], "silent.ts");
            }
            finally
            {
                player.Close();
                Stop(source);
            }
        }

        ReadStreams(segment).Select(s => s.Codec).Should().Equal(AVCodecID.H264);
    }

    // The newest segment is still being written; the ones before it are whole.
    private static List<string> FinishedSegments(string liveFolder)
    {
        var segments = Directory.GetFiles(liveFolder, "*.ts").Order(StringComparer.Ordinal).ToList();
        return segments.Count == 0 ? segments : segments[..^1];
    }

    // The stream removes its old segments; a copy stays to be looked at.
    private string Keep(
        string segment,
        string name)
    {
        var copy = Path.Combine(directory, name);
        File.Copy(segment, copy, overwrite: true);
        return copy;
    }

    [Theory]
    [InlineData(true, AVCodecID.H264)]
    [InlineData(false, AVCodecID.Hevc)]
    public async Task An_H265_Camera_Is_Recorded_As_H264_Only_When_That_Is_Asked_For(
        bool transcodeToH264,
        AVCodecID expectedCodec)
    {
        // H.265 halves the file, but not every player and no browser on
        // Windows takes it. Recording it as H.264 is a choice.
        TestEngine.Require();
        var tool = FindFFmpegTool();
        Assert.SkipWhen(tool is null, "The FFmpeg command-line tool is not installed.");

        var cancellationToken = TestContext.Current.CancellationToken;
        var port = GetFreePort();
        using var source = StartCompressedStream(tool!, port, "libx265");
        var path = Path.Combine(directory, $"h265-{transcodeToH264}.mkv");

        using (var player = new VideoPlayer(Microsoft.Extensions.Logging.Abstractions.NullLogger<VideoPlayer>.Instance))
        {
            try
            {
                player.Open(
                    new Uri($"tcp://127.0.0.1:{port}"),
                    new StreamOptions { HardwareAcceleration = false, TranscodeRecordingToH264 = transcodeToH264 });
                await WaitUntilAsync(() => player.State == PlayerState.Playing && player.FramesDecoded > 0, cancellationToken);
                player.StreamInfo!.CodecName.Should().Be("hevc");

                player.StartRecording(path);
                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
                player.StopRecording();
            }
            finally
            {
                player.Close();
                Stop(source);
            }
        }

        var recorded = ReadBack(path);
        recorded.Codec.Should().Be(expectedCodec);
        recorded.Frames.Should().BeGreaterThan(15);
        recorded.FirstIsKeyFrame.Should().BeTrue();
    }

    private static System.Diagnostics.Process StartCompressedStream(
        string tool,
        int port,
        string encoder)
    {
        var start = new System.Diagnostics.ProcessStartInfo
        {
            FileName = tool,
            Arguments =
                "-hide_banner -loglevel error -re -f lavfi -i testsrc=size=320x240:rate=15 " +
                $"-c:v {encoder} -preset ultrafast -tune zerolatency -g 15 -pix_fmt yuv420p -t 60 " +
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

    // Paced like a camera (-re): a file would be read to its end at once.
    private static System.Diagnostics.Process StartRawVideoStream(
        string tool,
        int port,
        bool withSound = false)
    {
        var sound = withSound ? "-re -f lavfi -i sine=frequency=440:sample_rate=44100 -c:a pcm_s16le -ac 2 " : string.Empty;

        var start = new System.Diagnostics.ProcessStartInfo
        {
            FileName = tool,
            Arguments =
                "-hide_banner -loglevel error -re -f lavfi -i testsrc=size=320x240:rate=15 " + sound +
                $"-c:v rawvideo -pix_fmt yuyv422 -t 60 -f nut \"tcp://127.0.0.1:{port}?listen=1\"",
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        return System.Diagnostics.Process.Start(start)
            ?? throw new InvalidOperationException("The test stream could not be started.");
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

    private static unsafe Recorded ReadBack(string path)
    {
        AVFormatContext* context = null;
        AVDictionary* options = null;
        avformat_open_input(ref context, path, null, ref options).Should().Be(0, $"'{path}' has to be a readable recording");
        var packet = av_packet_alloc();
        try
        {
            avformat_find_stream_info(context, (AVDictionary**)null).Should().BeGreaterThanOrEqualTo(0);
            var codec = context->streams[0]->codecpar->codec_id;

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

            return new Recorded(codec, frames, firstIsKeyFrame);
        }
        finally
        {
            av_packet_free(ref packet);
            avformat_close_input(ref context);
        }
    }

    private static unsafe List<RecordedStream> ReadStreams(string path)
    {
        AVFormatContext* context = null;
        AVDictionary* options = null;
        avformat_open_input(ref context, path, null, ref options).Should().Be(0, $"'{path}' has to be a readable recording");
        var packet = av_packet_alloc();
        try
        {
            avformat_find_stream_info(context, (AVDictionary**)null).Should().BeGreaterThanOrEqualTo(0);
            var last = new double[context->nb_streams];
            while (av_read_frame(context, packet) >= 0)
            {
                var stream = context->streams[packet->stream_index];
                last[packet->stream_index] = (packet->pts + packet->duration) * av_q2d(stream->time_base);
                av_packet_unref(packet);
            }

            var streams = new List<RecordedStream>();
            for (var i = 0; i < last.Length; i++)
            {
                streams.Add(new RecordedStream(context->streams[i]->codecpar->codec_id, last[i]));
            }

            return streams;
        }
        finally
        {
            av_packet_free(ref packet);
            avformat_close_input(ref context);
        }
    }

    private sealed record RecordedStream(
        AVCodecID Codec,
        double Seconds);

    private sealed record Recorded(
        AVCodecID Codec,
        int Frames,
        bool FirstIsKeyFrame);
}