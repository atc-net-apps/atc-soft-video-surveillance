namespace AtcSoft.VideoEngine.Recording;

/// <summary>
/// The recorder can also write what a browser plays: an HLS playlist with
/// short segments. Needs the FFmpeg shared libraries; skipped where they are
/// not installed.
/// </summary>
public sealed unsafe class RemuxerLiveStreamTests : IDisposable
{
    private const int Width = 320;
    private const int Height = 240;
    private const int FramesPerSecond = 30;

    private static readonly AVRational SourceTimeBase = new() { Num = 1, Den = 1_000_000 };

    private readonly string directory = Path.Combine(Path.GetTempPath(), "atcsoft-live-" + Guid.NewGuid().ToString("N"));

    public RemuxerLiveStreamTests()
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
    public void A_Live_Stream_Is_A_Playlist_With_Segments_Of_Two_Seconds()
    {
        TestEngine.Require();
        var playlist = Path.Combine(directory, "stream.m3u8");

        using var stream = WriteLiveStream(playlist, seconds: 7, close: true);

        var lines = File.ReadAllLines(playlist);
        lines.Should().Contain("#EXTM3U");
        var segments = lines.Where(line => line.EndsWith(".ts", StringComparison.Ordinal)).ToList();
        segments.Should().HaveCountGreaterThanOrEqualTo(3);
        segments.Should().OnlyContain(segment => File.Exists(Path.Combine(directory, segment)));
        lines.Where(line => line.StartsWith("#EXTINF:", StringComparison.Ordinal))
            .Take(2)
            .Should().OnlyContain(line => line.StartsWith("#EXTINF:2.0", StringComparison.Ordinal));
    }

    [Fact]
    public void The_Playlist_Is_There_While_The_Stream_Is_Still_Running()
    {
        // A browser starts playing as soon as the first segments are listed.
        TestEngine.Require();
        var playlist = Path.Combine(directory, "stream.m3u8");

        using var stream = WriteLiveStream(playlist, seconds: 5, close: false);

        File.Exists(playlist).Should().BeTrue();
        File.ReadAllText(playlist).Should().Contain(".ts").And.NotContain("#EXT-X-ENDLIST");
    }

    [Fact]
    public void Every_Segment_Can_Be_Played_On_Its_Own()
    {
        // A browser that joins late starts with whatever segment is current.
        // The encoder keeps the stream's parameters for the file header;
        // a segment needs them in front of its first picture.
        TestEngine.Require();
        var playlist = Path.Combine(directory, "stream.m3u8");

        using var stream = WriteLiveStream(playlist, seconds: 7, close: true);

        var later = Directory.GetFiles(directory, "*.ts").OrderBy(path => path, StringComparer.Ordinal).ElementAt(1);
        var (codec, width, height, firstIsKeyFrame) = Probe(later);
        codec.Should().Be(AVCodecID.H264);
        width.Should().Be(Width, "the size is only known from the parameters in the segment itself");
        height.Should().Be(Height);
        firstIsKeyFrame.Should().BeTrue();
    }

    [Fact]
    public void Old_Segments_Are_Removed_So_The_Folder_Does_Not_Grow()
    {
        TestEngine.Require();
        var playlist = Path.Combine(directory, "stream.m3u8");

        using var stream = WriteLiveStream(playlist, seconds: 30, close: true);

        Directory.GetFiles(directory, "*.ts").Length.Should().BeLessThan(10, "thirty seconds are fifteen segments");
    }

    private static Remuxer WriteLiveStream(
        string playlist,
        int seconds,
        bool close)
    {
        var remuxer = new Remuxer();
        using var encoder = new FrameEncoder();
        encoder.Open(Width, Height, AVPixelFormat.Yuyv422, FramesPerSecond);
        remuxer.OpenLive(playlist, encoder.CodecParameters, encoder.TimeBase);

        var frame = av_frame_alloc();
        try
        {
            frame->format = (int)AVPixelFormat.Yuyv422;
            frame->width = Width;
            frame->height = Height;
            av_frame_get_buffer(frame, 0).Should().Be(0);

            for (var i = 0; i < seconds * FramesPerSecond; i++)
            {
                av_frame_make_writable(frame).Should().Be(0);
                new Span<byte>((byte*)frame->data[0], frame->linesize[0] * Height).Fill((byte)(i * 3));
                frame->pts = 95_000_000_000L + (i * 1_000_000L / FramesPerSecond);
                encoder.Encode(frame, SourceTimeBase, packet => remuxer.WritePacket(packet, encoder.TimeBase));
            }
        }
        finally
        {
            av_frame_free(ref frame);
        }

        if (close)
        {
            remuxer.Close();
        }
        else
        {
            remuxer.Flush();
        }

        return remuxer;
    }

    private static (AVCodecID Codec, int Width, int Height, bool FirstIsKeyFrame) Probe(
        string path)
    {
        AVFormatContext* context = null;
        AVDictionary* options = null;
        avformat_open_input(ref context, path, null, ref options).Should().Be(0, $"'{path}' has to be readable");
        var packet = av_packet_alloc();
        try
        {
            avformat_find_stream_info(context, (AVDictionary**)null).Should().BeGreaterThanOrEqualTo(0);
            var parameters = context->streams[0]->codecpar;
            av_read_frame(context, packet).Should().Be(0);
            var isKey = (packet->flags & PktFlags.Key) != PktFlags.None;
            return (parameters->codec_id, parameters->width, parameters->height, isKey);
        }
        finally
        {
            av_packet_free(ref packet);
            avformat_close_input(ref context);
        }
    }
}