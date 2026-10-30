namespace AtcSoft.VideoEngine.Recording;

/// <summary>
/// A recording must survive the recorder dying mid-file (a crash or a power
/// cut): what was written so far has to stay readable.
/// Needs the FFmpeg shared libraries; skipped where they are not installed,
/// failed where they are installed but do not load.
/// </summary>
public sealed unsafe class RemuxerCrashSafetyTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "atcsoft-remuxer-crash-" + Guid.NewGuid().ToString("N"));

    public RemuxerCrashSafetyTests()
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
    [InlineData("mp4")]
    [InlineData("mkv")]
    public void A_Recording_That_Was_Never_Closed_Is_Still_Readable(
        string format)
    {
        TestEngine.Require();

        var path = Path.Combine(directory, $"unfinished.{format}");
        var snapshot = Path.Combine(directory, $"as-found-after-a-crash.{format}");

        var codecpar = avcodec_parameters_alloc();
        var packet = av_packet_alloc();
        try
        {
            codecpar->codec_type = AVMediaType.Video;
            codecpar->codec_id = AVCodecID.Mpeg4;
            codecpar->width = 320;
            codecpar->height = 240;
            codecpar->format = (int)AVPixelFormat.Yuv420p;
            var timeBase = new AVRational { Num = 1, Den = 25 };

            using var remuxer = new Remuxer();
            remuxer.Open(path, codecpar, timeBase, rotationDegrees: 90);

            av_new_packet(packet, 2048).Should().Be(0);
            new Span<byte>(packet->data, packet->size).Clear();
            packet->flags = PktFlags.Key;

            // Sixteen seconds of video: long enough that the muxers have
            // closed several clusters / fragments, as they do in a real recording.
            const int packetCount = 400;
            for (var i = 0; i < packetCount; i++)
            {
                packet->pts = i;
                packet->dts = i;
                remuxer.WritePacket(packet, timeBase);
            }

            // Take the file exactly as a crash would leave it: the recorder
            // is still open and has written no trailer.
            WaitUntilWritten(path, minimumBytes: packetCount / 2 * 2048);
            CopyWhileOpen(path, snapshot);
        }
        finally
        {
            av_packet_free(ref packet);
            avcodec_parameters_free(ref codecpar);
        }

        // Most of the video has reached the disk...
        new FileInfo(snapshot).Length.Should().BeGreaterThan(200 * 2048);

        // ...and the file can be opened. The rotation flag lives in the file
        // header, so reading it back shows the header is there and intact.
        MediaProbe.GetRotation(snapshot).Should().Be(90);
    }

    private static void WaitUntilWritten(
        string path,
        long minimumBytes)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (stream.Length >= minimumBytes)
                {
                    return;
                }
            }

            Thread.Sleep(20);
        }
    }

    private static void CopyWhileOpen(
        string source,
        string destination)
    {
        using var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var output = File.Create(destination);
        input.CopyTo(output);
    }
}