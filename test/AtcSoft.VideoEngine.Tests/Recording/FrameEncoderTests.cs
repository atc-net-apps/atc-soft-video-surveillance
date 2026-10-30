namespace AtcSoft.VideoEngine.Recording;

/// <summary>
/// Records uncompressed pictures, as a webcam delivers them, through the
/// encoder and the recorder, and reads the files back. Needs the FFmpeg
/// shared libraries; skipped where they are not installed.
/// </summary>
public sealed unsafe class FrameEncoderTests : IDisposable
{
    private const int Width = 320;
    private const int Height = 240;
    private const int FramesPerSecond = 30;
    private const long CameraStart = 95_000L * 10_000_000;

    // DirectShow counts in 100 ns steps from when the machine started.
    private static readonly AVRational CameraTimeBase = new() { Num = 1, Den = 10_000_000 };

    private readonly string directory = Path.Combine(Path.GetTempPath(), "atcsoft-encoder-" + Guid.NewGuid().ToString("N"));

    public FrameEncoderTests()
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
    [InlineData(AVCodecID.Rawvideo, true)]
    [InlineData(AVCodecID.H264, false)]
    [InlineData(AVCodecID.Hevc, false)]
    [InlineData(AVCodecID.Mjpeg, false)]
    public void Only_Uncompressed_Video_Is_Encoded(
        AVCodecID codec,
        bool expected)
        => FrameEncoder.IsNeededFor(codec).Should().Be(expected);

    [Theory]
    [InlineData(AVCodecID.Hevc, true)]
    [InlineData(AVCodecID.Mjpeg, true)]
    [InlineData(AVCodecID.Rawvideo, true)]
    [InlineData(AVCodecID.H264, false)]
    public void When_Recordings_Are_Wanted_As_H264_Everything_Else_Is_Encoded(
        AVCodecID codec,
        bool expected)
        => FrameEncoder.IsNeededFor(codec, transcodeToH264: true).Should().Be(expected, "H.264 is copied as it is: encoding it again would only cost time and quality");

    [Fact]
    public void A_Picture_In_Another_Layout_Than_The_Encoder_Was_Opened_For_Is_Still_Encoded()
    {
        // A camera decoded on the graphics card hands its pictures over in
        // the card's layout, which is only known when the first one arrives.
        TestEngine.Require();
        var path = Path.Combine(directory, "other-layout.mkv");

        using (var encoder = new FrameEncoder())
        using (var remuxer = new Remuxer())
        {
            encoder.Open(Width, Height, AVPixelFormat.Yuv420p, FramesPerSecond);
            remuxer.Open(path, encoder.CodecParameters, encoder.TimeBase);

            EncodeFrames(encoder, remuxer, AVPixelFormat.Nv12, first: 0, count: 30);
            encoder.Flush(packet => remuxer.WritePacket(packet, encoder.TimeBase));
            remuxer.Close();
        }

        ReadBack(path).Frames.Should().Be(30);
    }

    [Theory]
    [InlineData("mp4", AVPixelFormat.Yuyv422)]
    [InlineData("mkv", AVPixelFormat.Yuyv422)]
    [InlineData("mp4", AVPixelFormat.Nv12)]
    [InlineData("mkv", AVPixelFormat.Nv12)]
    public void Uncompressed_Pictures_Are_Recorded_As_H264_That_Players_Take(
        string format,
        AVPixelFormat cameraFormat)
    {
        TestEngine.Require();
        var path = Path.Combine(directory, $"{cameraFormat}.{format}");

        using (var encoder = new FrameEncoder())
        using (var remuxer = new Remuxer())
        {
            encoder.Open(Width, Height, cameraFormat, FramesPerSecond);
            remuxer.Open(path, encoder.CodecParameters, encoder.TimeBase, rotationDegrees: 90);

            EncodeFrames(encoder, remuxer, cameraFormat, first: 0, count: 60);
            encoder.Flush(packet => remuxer.WritePacket(packet, encoder.TimeBase));
            remuxer.Close();
        }

        var recorded = ReadBack(path);
        recorded.Codec.Should().Be(AVCodecID.H264);
        recorded.PixelFormat.Should().Be(AVPixelFormat.Yuv420p, "browsers and most players take no other colour layout");
        recorded.Frames.Should().Be(60);
        recorded.FirstIsKeyFrame.Should().BeTrue();
        MediaProbe.GetDuration(path).TotalSeconds.Should().BeApproximately(2, 0.2);
        MediaProbe.GetRotation(path).Should().Be(90, "the pictures stay as the camera sent them; the file says how to turn them");
    }

    [Fact]
    public void A_New_File_Starts_With_A_Key_Frame_When_One_Is_Asked_For()
    {
        // The recorder drops everything up to the first key frame. Without
        // asking for one, a new hourly file would lose up to two seconds.
        TestEngine.Require();
        var first = Path.Combine(directory, "first.mkv");
        var second = Path.Combine(directory, "second.mkv");

        using (var encoder = new FrameEncoder())
        using (var remuxer = new Remuxer())
        {
            encoder.Open(Width, Height, AVPixelFormat.Yuyv422, FramesPerSecond);
            remuxer.Open(first, encoder.CodecParameters, encoder.TimeBase);
            EncodeFrames(encoder, remuxer, AVPixelFormat.Yuyv422, first: 0, count: 20);

            encoder.RequestKeyFrame();
            remuxer.SwitchTo(second, encoder.CodecParameters, encoder.TimeBase);
            EncodeFrames(encoder, remuxer, AVPixelFormat.Yuyv422, first: 20, count: 20);
            remuxer.Close();
        }

        ReadBack(first).Frames.Should().Be(20);
        var afterSwitch = ReadBack(second);
        afterSwitch.Frames.Should().Be(20, "no picture is lost waiting for a key frame");
        afterSwitch.FirstIsKeyFrame.Should().BeTrue();
    }

    [Fact]
    public void A_Picture_With_A_Time_That_Does_Not_Move_On_Is_Left_Out()
    {
        // A webcam now and then delivers two pictures with the same time;
        // an encoder refuses the second one.
        TestEngine.Require();
        var path = Path.Combine(directory, "repeated.mkv");

        using (var encoder = new FrameEncoder())
        using (var remuxer = new Remuxer())
        {
            encoder.Open(Width, Height, AVPixelFormat.Yuyv422, FramesPerSecond);
            remuxer.Open(path, encoder.CodecParameters, encoder.TimeBase);

            EncodeFrames(encoder, remuxer, AVPixelFormat.Yuyv422, first: 0, count: 10);
            EncodeFrames(encoder, remuxer, AVPixelFormat.Yuyv422, first: 9, count: 1);
            EncodeFrames(encoder, remuxer, AVPixelFormat.Yuyv422, first: 10, count: 10);
            encoder.Flush(packet => remuxer.WritePacket(packet, encoder.TimeBase));
            remuxer.Close();
        }

        ReadBack(path).Frames.Should().Be(20);
    }

    [Fact]
    public void A_Picture_Of_An_Odd_Size_Is_Recorded_With_Even_Sides()
    {
        // H.264 with 4:2:0 colour needs even sides.
        TestEngine.Require();

        using var encoder = new FrameEncoder();
        encoder.Open(321, 241, AVPixelFormat.Yuyv422, FramesPerSecond);

        encoder.CodecParameters->width.Should().Be(320);
        encoder.CodecParameters->height.Should().Be(240);
    }

    private static void EncodeFrames(
        FrameEncoder encoder,
        Remuxer remuxer,
        AVPixelFormat cameraFormat,
        int first,
        int count)
    {
        var frame = av_frame_alloc();
        try
        {
            frame->format = (int)cameraFormat;
            frame->width = Width;
            frame->height = Height;
            av_frame_get_buffer(frame, 0).Should().Be(0);

            for (var i = first; i < first + count; i++)
            {
                av_frame_make_writable(frame).Should().Be(0);
                Fill(frame, (byte)(i * 4));
                frame->pts = CameraStart + (i * (CameraTimeBase.Den / FramesPerSecond));

                encoder.Encode(frame, CameraTimeBase, packet => remuxer.WritePacket(packet, encoder.TimeBase));
            }
        }
        finally
        {
            av_frame_free(ref frame);
        }
    }

    // A grey level that changes from picture to picture, so the encoder has work to do.
    private static void Fill(
        AVFrame* frame,
        byte level)
    {
        var luma = new Span<byte>((byte*)frame->data[0], frame->linesize[0] * frame->height);
        luma.Fill(level);

        if (frame->format == (int)AVPixelFormat.Nv12)
        {
            new Span<byte>((byte*)frame->data[1], frame->linesize[1] * frame->height / 2).Fill(128);
        }
    }

    private static Recorded ReadBack(string path)
    {
        AVFormatContext* context = null;
        AVDictionary* options = null;
        avformat_open_input(ref context, path, null, ref options).Should().Be(0, $"'{path}' has to be a readable recording");
        var packet = av_packet_alloc();
        try
        {
            avformat_find_stream_info(context, (AVDictionary**)null).Should().BeGreaterThanOrEqualTo(0);
            context->nb_streams.Should().Be(1);
            var parameters = context->streams[0]->codecpar;

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

            return new Recorded(parameters->codec_id, (AVPixelFormat)parameters->format, frames, firstIsKeyFrame);
        }
        finally
        {
            av_packet_free(ref packet);
            avformat_close_input(ref context);
        }
    }

    private sealed record Recorded(
        AVCodecID Codec,
        AVPixelFormat PixelFormat,
        int Frames,
        bool FirstIsKeyFrame);
}