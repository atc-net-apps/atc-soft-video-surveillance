namespace AtcSoft.Video.Surveillance.Helpers;

public sealed class OnvifStreamChoiceTests
{
    [Fact]
    public void The_Largest_Picture_Is_The_Main_Stream_And_The_Smallest_Usable_One_The_Sub_Stream()
    {
        // A Hikvision-type camera: the 640x480 sub-stream saves more than the 1280x720 third stream.
        var choice = OnvifStreamChoice.Choose(
        [
            Profile("main", 2560, 1440, "/Streaming/Channels/101"),
            Profile("sub", 640, 480, "/Streaming/Channels/102"),
            Profile("third", 1280, 720, "/Streaming/Channels/103"),
            Profile("tiny", 176, 144, "/Streaming/Channels/104"),
        ])!;

        choice.Main.Token.Should().Be("main");
        choice.Sub!.Token.Should().Be("sub", "the tiny one is too small for motion detection");
    }

    [Fact]
    public void Of_Two_As_Small_The_One_Of_The_Main_Shape_Is_The_Sub_Stream()
        => OnvifStreamChoice.Choose(
            [
                Profile("main", 1920, 1080, "/main"),
                Profile("square", 480, 480, "/a"),
                Profile("wide", 640, 360, "/b"),
            ])!
            .Sub!.Token.Should().Be("wide", "480x480 and 640x360 are as large; 640x360 has the main stream's shape");

    [Fact]
    public void A_Squeezed_Sub_Stream_Is_Taken_When_There_Is_None_Of_The_Main_Shape()
    {
        // Squeezed, the zones and boxes still land right: the analysis takes the main shape.
        var choice = OnvifStreamChoice.Choose(
        [
            Profile("main", 2560, 1440, "/main"),
            Profile("sub", 640, 480, "/sub"),
        ])!;

        choice.Sub!.Token.Should().Be("sub");
    }

    [Fact]
    public void A_Second_Stream_Nearly_As_Large_Is_No_Sub_Stream()
        => OnvifStreamChoice.Choose([Profile("main", 1920, 1080, "/a"), Profile("other", 1280, 720, "/b")])!
            .Sub.Should().BeNull("it would save little");

    [Fact]
    public void Mjpeg_Is_The_Main_Stream_Only_When_Nothing_Else_Is_Offered()
    {
        OnvifStreamChoice.Choose([Profile("jpeg", 1920, 1080, "/mjpeg", "JPEG"), Profile("h264", 1280, 720, "/h264")])!
            .Main.Token.Should().Be("h264");
        OnvifStreamChoice.Choose([Profile("jpeg", 1280, 720, "/mjpeg", "JPEG")])!
            .Main.Token.Should().Be("jpeg");
    }

    [Fact]
    public void Without_An_Rtsp_Address_There_Is_No_Choice()
        => OnvifStreamChoice.Choose([Profile("http", 1920, 1080, "/x") with { StreamUri = new Uri("http://10.0.0.99/x") }])
            .Should().BeNull();

    [Fact]
    public void The_Choice_Fills_In_The_Connection_And_Keeps_The_Typed_Address()
    {
        var connection = new ConnectionSettings { IpAddress = "camera.local", Protocol = CameraProtocol.Http, Port = 80 };
        var choice = OnvifStreamChoice.Choose(
        [
            Profile("main", 1920, 1080, "/cam/realmonitor?channel=1&subtype=0"),
            Profile("sub", 704, 576, "/cam/realmonitor?channel=1&subtype=1", port: 8554),
        ])!;

        choice.ApplyTo(connection);

        connection.IpAddress.Should().Be("camera.local", "the camera may name another of its addresses");
        (connection.Protocol, connection.Port, connection.Path).Should().Be((CameraProtocol.Rtsp, 554, "/cam/realmonitor?channel=1&subtype=0"));
        (connection.SubStreamPath, connection.SubStreamPort).Should().Be(("/cam/realmonitor?channel=1&subtype=1", 8554));
    }

    [Fact]
    public void A_Camera_With_One_Stream_Loses_An_Old_Sub_Stream()
    {
        var connection = new ConnectionSettings { SubStreamPath = "/old", SubStreamPort = 8554 };

        OnvifStreamChoice.Choose([Profile("only", 1280, 720, "/live")])!.ApplyTo(connection);

        (connection.SubStreamPath, connection.SubStreamPort).Should().Be((null, null));
    }

    private static OnvifStreamProfile Profile(
        string token,
        int width,
        int height,
        string pathAndQuery,
        string encoding = "H264",
        int port = 554)
        => new(token, token, encoding, width, height, new Uri($"rtsp://10.0.0.99:{port}{pathAndQuery}"));
}