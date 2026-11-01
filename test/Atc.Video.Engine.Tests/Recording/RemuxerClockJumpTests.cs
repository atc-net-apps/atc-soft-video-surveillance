namespace Atc.Video.Engine.Recording;

/// <summary>
/// A camera's clock can jump: a resync, or a wrap (MPEG-TS every 26.5 h, RTP every 13.25 h). The
/// recording goes on one picture after the last. Before, every packet after a jump back was put one
/// tick after the one before it: the rest of the file was squeezed into a moment.
/// </summary>
public sealed unsafe class RemuxerClockJumpTests : IDisposable
{
    private const int PacketsBefore = 50;
    private const int PacketsAfter = 50;
    private const long TicksPerPicture = 3600;

    private static readonly AVRational TimeBase = new() { Num = 1, Den = 90_000 };

    private readonly string directory = Path.Combine(Path.GetTempPath(), "atcsoft-clockjump-" + Guid.NewGuid().ToString("N"));

    public RemuxerClockJumpTests()
        => Directory.CreateDirectory(directory);

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
    [InlineData(-8_589_934_592L)]
    [InlineData(3_600L * 90_000)]
    public void A_Jump_In_The_Cameras_Clock_Is_Recorded_As_The_Next_Picture(
        long jump)
    {
        TestEngine.Require();
        var path = Path.Combine(directory, "jump.mkv");

        int clockJumps;
        using (var remuxer = new Remuxer())
        {
            Open(remuxer, path);

            // 25 pictures a second; after 2 s the clock wraps back (or leaps an hour ahead).
            var start = 8_589_934_592L - (PacketsBefore * TicksPerPicture);
            Write(remuxer, start, PacketsBefore);
            Write(remuxer, start + (PacketsBefore * TicksPerPicture) + jump, PacketsAfter);
            remuxer.Close();
            clockJumps = remuxer.ClockJumps;
        }

        var times = ReadPacketSeconds(path);
        times.Should().HaveCount(PacketsBefore + PacketsAfter);
        times[^1].Should().BeApproximately((PacketsBefore + PacketsAfter - 1) * 0.04, 0.01, "four seconds of pictures make a four-second file");
        times.Zip(times.Skip(1), (a, b) => b - a).Should().AllSatisfy(step => step.Should().BeApproximately(0.04, 0.005));
        clockJumps.Should().Be(1);
    }

    private static void Open(
        Remuxer remuxer,
        string path)
    {
        var codecpar = avcodec_parameters_alloc();
        try
        {
            codecpar->codec_type = AVMediaType.Video;
            codecpar->codec_id = AVCodecID.Mpeg4;
            codecpar->width = 320;
            codecpar->height = 240;
            codecpar->format = (int)AVPixelFormat.Yuv420p;
            remuxer.Open(path, codecpar, TimeBase);
        }
        finally
        {
            avcodec_parameters_free(ref codecpar);
        }
    }

    private static void Write(
        Remuxer remuxer,
        long firstTimestamp,
        int count)
    {
        var packet = av_packet_alloc();
        try
        {
            // The recorder copies packets without looking inside them.
            av_new_packet(packet, 64).Should().Be(0);
            new Span<byte>(packet->data, packet->size).Clear();
            packet->flags = PktFlags.Key;

            for (var i = 0; i < count; i++)
            {
                packet->pts = firstTimestamp + (i * TicksPerPicture);
                packet->dts = packet->pts;
                remuxer.WritePacket(packet, TimeBase);
            }
        }
        finally
        {
            av_packet_free(ref packet);
        }
    }

    private static List<double> ReadPacketSeconds(string path)
    {
        AVFormatContext* context = null;
        AVDictionary* options = null;
        avformat_open_input(ref context, path, null, ref options).Should().Be(0);
        var packet = av_packet_alloc();
        try
        {
            var timeBase = context->streams[0]->time_base;
            var times = new List<double>();
            while (av_read_frame(context, packet) >= 0)
            {
                times.Add(packet->pts * (double)timeBase.Num / timeBase.Den);
                av_packet_unref(packet);
            }

            return times;
        }
        finally
        {
            av_packet_free(ref packet);
            avformat_close_input(ref context);
        }
    }
}