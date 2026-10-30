namespace AtcSoft.VideoEngine.Demuxing;

public class DemuxerOptionPairsTests
{
    [Fact]
    public void Auto_Network_Sets_RtspTransport_Probesize_Analyzeduration_Timeout()
    {
        var options = new StreamOptions { RtspTransport = "tcp", UseLowLatencyMode = false };

        var pairs = Demuxer.BuildAvOptionPairs(options, isFFmpegV8: true);

        var dict = ToDict(pairs);
        dict["rtsp_transport"].Should().Be("tcp");
        dict["probesize"].Should().Be("50000000");
        dict["analyzeduration"].Should().Be("10000000");
        dict["timeout"].Should().Be("15000000");
        dict.Should().NotContainKey("stimeout");
        dict.Should().NotContainKey("fflags");
    }

    [Fact]
    public void Auto_Network_Does_Not_Verify_The_Certificate_Unless_Asked()
    {
        ToDict(Demuxer.BuildAvOptionPairs(new StreamOptions(), isFFmpegV8: true))["tls_verify"].Should().Be("0");
        ToDict(Demuxer.BuildAvOptionPairs(new StreamOptions { VerifyTlsCertificate = true }, isFFmpegV8: true))["tls_verify"].Should().Be("1");
    }

    [Fact]
    public void Auto_Network_FFmpegV7_Uses_Stimeout()
    {
        var options = new StreamOptions { UseLowLatencyMode = false };

        var pairs = Demuxer.BuildAvOptionPairs(options, isFFmpegV8: false);

        var dict = ToDict(pairs);
        dict.Should().ContainKey("stimeout");
        dict.Should().NotContainKey("timeout");
    }

    [Fact]
    public void Auto_Network_LowLatency_Adds_Fflags_And_Flags()
    {
        var options = new StreamOptions { UseLowLatencyMode = true };

        var pairs = Demuxer.BuildAvOptionPairs(options, isFFmpegV8: true);

        var dict = ToDict(pairs);
        dict["fflags"].Should().Be("nobuffer");
        dict["flags"].Should().Be("low_delay");
    }

    [Fact]
    public void Dshow_Sets_Rtbufsize_Plus_Format_Triple()
    {
        var options = new StreamOptions
        {
            InputFormat = InputFormatKind.Dshow,
            RawDeviceSpec = "video=Logitech BRIO",
            VideoSize = "1920x1080",
            FrameRate = "30",
            PixelFormat = "nv12",
        };

        var pairs = Demuxer.BuildAvOptionPairs(options, isFFmpegV8: true);

        var dict = ToDict(pairs);
        dict["rtbufsize"].Should().Be("100000000");
        dict["video_size"].Should().Be("1920x1080");
        dict["framerate"].Should().Be("30");
        dict["pixel_format"].Should().Be("nv12");
        dict.Should().NotContainKey("rtsp_transport");
        dict.Should().NotContainKey("timeout");
    }

    [Fact]
    public void Dshow_Without_Format_Triple_Omits_Optional_Keys()
    {
        var options = new StreamOptions
        {
            InputFormat = InputFormatKind.Dshow,
            RawDeviceSpec = "video=Cam",
        };

        var pairs = Demuxer.BuildAvOptionPairs(options, isFFmpegV8: true);

        var dict = ToDict(pairs);
        dict.Should().ContainKey("rtbufsize");
        dict.Should().NotContainKey("video_size");
        dict.Should().NotContainKey("framerate");
        dict.Should().NotContainKey("pixel_format");
    }

    [Fact]
    public void V4l2_Maps_PixelFormat_To_InputFormat_Key()
    {
        // v4l2's option is `input_format`, not `pixel_format` — make
        // sure the helper translates correctly so we don't ship a bug
        // the day Linux support arrives.
        var options = new StreamOptions
        {
            InputFormat = InputFormatKind.V4l2,
            RawDeviceSpec = "/dev/video0",
            VideoSize = "640x480",
            FrameRate = "30",
            PixelFormat = "mjpeg",
        };

        var pairs = Demuxer.BuildAvOptionPairs(options, isFFmpegV8: true);

        var dict = ToDict(pairs);
        dict["video_size"].Should().Be("640x480");
        dict["framerate"].Should().Be("30");
        dict["input_format"].Should().Be("mjpeg");
        dict.Should().NotContainKey("pixel_format");
    }

    [Fact]
    public void AVFoundation_Uses_PixelFormat_Key()
    {
        var options = new StreamOptions
        {
            InputFormat = InputFormatKind.AVFoundation,
            RawDeviceSpec = "0",
            VideoSize = "1280x720",
            PixelFormat = "uyvy422",
        };

        var pairs = Demuxer.BuildAvOptionPairs(options, isFFmpegV8: true);

        var dict = ToDict(pairs);
        dict["video_size"].Should().Be("1280x720");
        dict["pixel_format"].Should().Be("uyvy422");
    }

    [Fact]
    public void InputFormatName_Returns_Expected_Names()
    {
        new StreamOptions { InputFormat = InputFormatKind.Auto }.InputFormatName.Should().BeNull();
        new StreamOptions { InputFormat = InputFormatKind.Dshow }.InputFormatName.Should().Be("dshow");
        new StreamOptions { InputFormat = InputFormatKind.V4l2 }.InputFormatName.Should().Be("v4l2");
        new StreamOptions { InputFormat = InputFormatKind.AVFoundation }.InputFormatName.Should().Be("avfoundation");
    }

    private static Dictionary<string, string> ToDict(
        IReadOnlyList<KeyValuePair<string, string>> pairs)
        => pairs.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
}