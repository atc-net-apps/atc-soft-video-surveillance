namespace AtcSoft.VideoEngine.Recording;

/// <summary>
/// Writes real files through the recorder and reads the rotation flag back.
/// Needs the FFmpeg shared libraries; skipped where they are not installed,
/// failed where they are installed but do not load.
/// </summary>
public sealed unsafe class RemuxerRotationTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "atcsoft-remuxer-" + Guid.NewGuid().ToString("N"));

    public RemuxerRotationTests()
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
    [InlineData("mp4", 0)]
    [InlineData("mp4", 90)]
    [InlineData("mp4", 180)]
    [InlineData("mp4", 270)]
    [InlineData("mkv", 0)]
    [InlineData("mkv", 90)]
    [InlineData("mkv", 180)]
    [InlineData("mkv", 270)]
    public void Recording_Carries_The_Rotation_It_Was_Opened_With(
        string format,
        int rotationDegrees)
    {
        TestEngine.Require();

        var path = Path.Combine(directory, $"rotation-{rotationDegrees}.{format}");

        WriteRecording(path, rotationDegrees);

        MediaProbe.GetRotation(path).Should().Be(rotationDegrees);
    }

    [Fact]
    public void GetRotation_Returns_Zero_For_A_File_That_Is_Not_Media()
    {
        TestEngine.Require();

        var path = Path.Combine(directory, "not-media.mp4");
        File.WriteAllText(path, "not a video");

        MediaProbe.GetRotation(path).Should().Be(0);
    }

    private static void WriteRecording(
        string path,
        int rotationDegrees)
    {
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
            remuxer.Open(path, codecpar, timeBase, rotationDegrees);

            // The recorder copies packets without looking inside them, so
            // the payload does not have to be a decodable picture.
            av_new_packet(packet, 64).Should().Be(0);
            new Span<byte>(packet->data, packet->size).Clear();
            packet->flags = PktFlags.Key;

            for (var i = 0; i < 5; i++)
            {
                packet->pts = i;
                packet->dts = i;
                remuxer.WritePacket(packet, timeBase);
            }

            remuxer.Close();
        }
        finally
        {
            av_packet_free(ref packet);
            avcodec_parameters_free(ref codecpar);
        }
    }
}