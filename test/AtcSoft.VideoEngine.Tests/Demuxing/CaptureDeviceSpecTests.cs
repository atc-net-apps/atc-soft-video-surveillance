namespace AtcSoft.VideoEngine.Demuxing;

/// <summary>
/// A camera that is opened together with a microphone does not open at all
/// when the microphone is not there (it sat in a monitor or dock that is
/// not connected). It is then opened again for its picture alone.
/// </summary>
public class CaptureDeviceSpecTests
{
    [Theory]
    [InlineData("video=HP 5MP Camera:audio=Microphone (HP 534pm Audio)", "video=HP 5MP Camera", "Microphone (HP 534pm Audio)")]
    [InlineData("video=Cam: front:audio=Mic: left", "video=Cam: front", "Mic: left")]
    public void The_Picture_Alone_Is_What_Stands_Before_The_Microphone(
        string spec,
        string expectedVideoOnly,
        string expectedMicrophone)
    {
        var options = new StreamOptions { InputFormat = InputFormatKind.Dshow, RawDeviceSpec = spec };

        CaptureDeviceSpec.TryWithoutAudio(options, out var videoOnly, out var microphone).Should().BeTrue();

        videoOnly!.RawDeviceSpec.Should().Be(expectedVideoOnly);
        microphone.Should().Be(expectedMicrophone);
    }

    [Fact]
    public void Everything_Else_About_The_Camera_Stays_As_It_Was()
    {
        var options = new StreamOptions
        {
            InputFormat = InputFormatKind.Dshow,
            RawDeviceSpec = "video=Cam:audio=Mic",
            VideoSize = "1280x720",
            FrameRate = "30",
            Source = "Front Door",
            RecordAudio = true,
        };

        CaptureDeviceSpec.TryWithoutAudio(options, out var videoOnly, out _).Should().BeTrue();

        videoOnly.Should().NotBeSameAs(options, "the caller's options still name the microphone for the next attempt");
        options.RawDeviceSpec.Should().Be("video=Cam:audio=Mic");
        videoOnly!.InputFormat.Should().Be(InputFormatKind.Dshow);
        videoOnly.VideoSize.Should().Be("1280x720");
        videoOnly.FrameRate.Should().Be("30");
        videoOnly.Source.Should().Be("Front Door");
    }

    [Theory]
    [InlineData(InputFormatKind.Dshow, "video=HP 5MP Camera")]
    [InlineData(InputFormatKind.Dshow, "audio=Microphone")]
    [InlineData(InputFormatKind.Dshow, null)]
    [InlineData(InputFormatKind.Auto, "video=Cam:audio=Mic")]
    public void A_Source_Without_A_Microphone_To_Leave_Out_Has_No_Second_Way_To_Open(
        InputFormatKind inputFormat,
        string? spec)
    {
        var options = new StreamOptions { InputFormat = inputFormat, RawDeviceSpec = spec };

        CaptureDeviceSpec.TryWithoutAudio(options, out var videoOnly, out _).Should().BeFalse();

        videoOnly.Should().BeNull();
    }
}