namespace AtcSoft.VideoEngine.Recording;

/// <summary>
/// Packets are written by a background thread. Closing a file, or moving on
/// to the next one, must not lose the packet that thread has just picked up,
/// nor let it land in the wrong file.
/// </summary>
public sealed unsafe class RemuxerPacketLossTests : IDisposable
{
    private const int Rounds = 40;
    private const int PacketsPerFile = 25;

    private static readonly AVRational TimeBase = new() { Num = 1, Den = 25 };

    private readonly string directory = Path.Combine(Path.GetTempPath(), "atcsoft-remuxer-" + Guid.NewGuid().ToString("N"));

    public RemuxerPacketLossTests()
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
    public void Every_Packet_Written_Before_Close_Is_In_The_File()
    {
        TestEngine.Require();

        var counts = new List<int>();
        for (var round = 0; round < Rounds; round++)
        {
            var path = Path.Combine(directory, $"close-{round}.mkv");
            using var remuxer = new Remuxer();
            Open(remuxer, path, switchTo: false);

            WritePackets(remuxer, first: 0);
            remuxer.Close();

            counts.Add(CountPackets(path));
        }

        counts.Should().AllSatisfy(count => count.Should().Be(PacketsPerFile));
    }

    [Fact]
    public void Every_Packet_Lands_In_The_File_That_Was_Open_When_It_Was_Written()
    {
        TestEngine.Require();

        var counts = new List<(int Before, int After)>();
        for (var round = 0; round < Rounds; round++)
        {
            var before = Path.Combine(directory, $"switch-{round}-a.mkv");
            var after = Path.Combine(directory, $"switch-{round}-b.mkv");
            using var remuxer = new Remuxer();
            Open(remuxer, before, switchTo: false);

            WritePackets(remuxer, first: 0);
            Open(remuxer, after, switchTo: true);
            WritePackets(remuxer, first: PacketsPerFile);
            remuxer.Close();

            counts.Add((CountPackets(before), CountPackets(after)));
        }

        counts.Should().AllSatisfy(count => count.Should().Be((PacketsPerFile, PacketsPerFile)));
    }

    private static void Open(
        Remuxer remuxer,
        string path,
        bool switchTo)
    {
        var codecpar = avcodec_parameters_alloc();
        try
        {
            codecpar->codec_type = AVMediaType.Video;
            codecpar->codec_id = AVCodecID.Mpeg4;
            codecpar->width = 320;
            codecpar->height = 240;
            codecpar->format = (int)AVPixelFormat.Yuv420p;

            if (switchTo)
            {
                remuxer.SwitchTo(path, codecpar, TimeBase);
            }
            else
            {
                remuxer.Open(path, codecpar, TimeBase);
            }
        }
        finally
        {
            avcodec_parameters_free(ref codecpar);
        }
    }

    private static void WritePackets(
        Remuxer remuxer,
        int first)
    {
        var packet = av_packet_alloc();
        try
        {
            // The recorder copies packets without looking inside them.
            av_new_packet(packet, 64).Should().Be(0);
            new Span<byte>(packet->data, packet->size).Clear();
            packet->flags = PktFlags.Key;

            for (var i = first; i < first + PacketsPerFile; i++)
            {
                packet->pts = i;
                packet->dts = i;
                remuxer.WritePacket(packet, TimeBase);
            }
        }
        finally
        {
            av_packet_free(ref packet);
        }
    }

    private static int CountPackets(string path)
    {
        AVFormatContext* context = null;
        AVDictionary* options = null;
        avformat_open_input(ref context, path, null, ref options).Should().Be(0);
        var packet = av_packet_alloc();
        try
        {
            var count = 0;
            while (av_read_frame(context, packet) >= 0)
            {
                count++;
                av_packet_unref(packet);
            }

            return count;
        }
        finally
        {
            av_packet_free(ref packet);
            avformat_close_input(ref context);
        }
    }
}