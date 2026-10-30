namespace AtcSoft.VideoEngine.Recording;

/// <summary>
/// Records a microphone's uncompressed sound next to the picture and reads
/// the file back. Needs the FFmpeg shared libraries; skipped where they are
/// not installed.
/// </summary>
public sealed unsafe class AudioEncoderTests : IDisposable
{
    private const int SampleRate = 44_100;
    private const int Channels = 2;
    private const int SamplesPerPacket = 441;
    private const int VideoFramesPerSecond = 25;
    private const int Seconds = 2;

    // A camera and its microphone count from when the machine started.
    private const long SourceStartMicroseconds = 95_000L * 1_000_000;

    private static readonly AVRational Microseconds = new() { Num = 1, Den = 1_000_000 };

    private readonly string directory = Path.Combine(Path.GetTempPath(), "atcsoft-audio-" + Guid.NewGuid().ToString("N"));

    public AudioEncoderTests()
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
    public void Sound_Is_Recorded_As_Aac_Next_To_The_Picture(string format)
    {
        TestEngine.Require();
        var path = Path.Combine(directory, "sound." + format);

        Record(path, soundStartsAfterMicroseconds: 0);

        var streams = ReadBack(path);
        streams.Should().HaveCount(2);
        streams[0].Type.Should().Be(AVMediaType.Video);
        streams[1].Type.Should().Be(AVMediaType.Audio);
        streams[1].Codec.Should().Be(AVCodecID.Aac, "players and browsers take AAC in every container");
        streams[1].LastSecond.Should().BeApproximately(Seconds, 0.15);
        streams[0].LastSecond.Should().BeApproximately(Seconds, 0.15);

        // The apps read a recording's length and rotation from the file.
        MediaProbe.GetDuration(path).TotalSeconds.Should().BeApproximately(Seconds, 0.15);
        MediaProbe.GetRotation(path).Should().Be(0);
    }

    [Fact]
    public void Sound_Keeps_Its_Place_Against_The_Picture()
    {
        // The microphone starts half a second after the camera. In the file
        // the sound has to start half a second in, not at the beginning.
        TestEngine.Require();
        var path = Path.Combine(directory, "late-sound.mkv");

        Record(path, soundStartsAfterMicroseconds: 500_000);

        var streams = ReadBack(path);
        streams[0].FirstSecond.Should().BeApproximately(0, 0.05);
        streams[1].FirstSecond.Should().BeApproximately(0.5, 0.06);
    }

    [Fact]
    public void Sound_From_Before_The_First_Picture_Is_Left_Out()
    {
        // A file starts with its first key frame; sound from before it has
        // no picture to go with.
        TestEngine.Require();
        var path = Path.Combine(directory, "early-sound.mkv");

        Record(path, soundStartsAfterMicroseconds: -500_000);

        var streams = ReadBack(path);
        streams[1].FirstSecond.Should().BeInRange(0, 0.06);
    }

    private static void Record(
        string path,
        long soundStartsAfterMicroseconds)
    {
        var videoParameters = avcodec_parameters_alloc();
        var soundParameters = avcodec_parameters_alloc();
        var packet = av_packet_alloc();
        try
        {
            videoParameters->codec_type = AVMediaType.Video;
            videoParameters->codec_id = AVCodecID.Mpeg4;
            videoParameters->width = 320;
            videoParameters->height = 240;
            videoParameters->format = (int)AVPixelFormat.Yuv420p;

            soundParameters->codec_type = AVMediaType.Audio;
            soundParameters->codec_id = AVCodecID.PcmS16le;
            soundParameters->format = (int)AVSampleFormat.S16;
            soundParameters->sample_rate = SampleRate;
            av_channel_layout_default(&soundParameters->ch_layout, Channels);

            using var encoder = new AudioEncoder();
            using var remuxer = new Remuxer();
            encoder.Open(soundParameters);
            remuxer.Open(path, videoParameters, Microseconds, rotationDegrees: 0, encoder.CodecParameters);

            // Sound and picture arrive interleaved, as from a camera.
            var soundPackets = Seconds * SampleRate / SamplesPerPacket;
            var soundPerVideo = soundPackets / (Seconds * VideoFramesPerSecond);
            var sound = 0;
            for (var frame = 0; frame < Seconds * VideoFramesPerSecond; frame++)
            {
                WriteVideoPacket(remuxer, packet, frame);

                for (var i = 0; i < soundPerVideo; i++, sound++)
                {
                    WriteSoundPacket(encoder, remuxer, packet, sound, soundStartsAfterMicroseconds);
                }
            }

            encoder.Flush(encoded => remuxer.WriteAudioPacket(encoded, encoder.TimeBase));
            remuxer.Close();
        }
        finally
        {
            av_packet_free(ref packet);
            avcodec_parameters_free(ref soundParameters);
            avcodec_parameters_free(ref videoParameters);
        }
    }

    private static void WriteVideoPacket(
        Remuxer remuxer,
        AVPacket* packet,
        int frame)
    {
        // The recorder copies packets without looking inside them.
        av_new_packet(packet, 64).Should().Be(0);
        new Span<byte>(packet->data, packet->size).Clear();
        packet->flags = PktFlags.Key;
        packet->pts = SourceStartMicroseconds + (frame * 1_000_000L / VideoFramesPerSecond);
        packet->dts = packet->pts;
        remuxer.WritePacket(packet, Microseconds);
        av_packet_unref(packet);
    }

    private static void WriteSoundPacket(
        AudioEncoder encoder,
        Remuxer remuxer,
        AVPacket* packet,
        int index,
        long startsAfterMicroseconds)
    {
        av_new_packet(packet, SamplesPerPacket * Channels * sizeof(short)).Should().Be(0);

        // A 440 Hz tone, so the encoder has something to encode.
        var samples = new Span<short>(packet->data, SamplesPerPacket * Channels);
        for (var i = 0; i < SamplesPerPacket; i++)
        {
            var value = (short)(Math.Sin(2 * Math.PI * 440 * ((index * SamplesPerPacket) + i) / SampleRate) * 8000);
            samples[i * Channels] = value;
            samples[(i * Channels) + 1] = value;
        }

        packet->pts = SourceStartMicroseconds + startsAfterMicroseconds + (index * 1_000_000L * SamplesPerPacket / SampleRate);
        packet->dts = packet->pts;
        encoder.Encode(packet, Microseconds, encoded => remuxer.WriteAudioPacket(encoded, encoder.TimeBase));
        av_packet_unref(packet);
    }

    private static List<RecordedStream> ReadBack(string path)
    {
        AVFormatContext* context = null;
        AVDictionary* options = null;
        avformat_open_input(ref context, path, null, ref options).Should().Be(0, $"'{path}' has to be a readable recording");
        var packet = av_packet_alloc();
        try
        {
            avformat_find_stream_info(context, (AVDictionary**)null).Should().BeGreaterThanOrEqualTo(0);

            var count = (int)context->nb_streams;
            var first = new double[count];
            var last = new double[count];
            Array.Fill(first, double.NaN);

            while (av_read_frame(context, packet) >= 0)
            {
                var stream = context->streams[packet->stream_index];
                var second = (packet->pts + packet->duration) * av_q2d(stream->time_base);
                if (double.IsNaN(first[packet->stream_index]))
                {
                    first[packet->stream_index] = packet->pts * av_q2d(stream->time_base);
                }

                last[packet->stream_index] = second;
                av_packet_unref(packet);
            }

            var streams = new List<RecordedStream>();
            for (var i = 0; i < count; i++)
            {
                var parameters = context->streams[i]->codecpar;
                streams.Add(new RecordedStream(parameters->codec_type, parameters->codec_id, first[i], last[i]));
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
        AVMediaType Type,
        AVCodecID Codec,
        double FirstSecond,
        double LastSecond);
}