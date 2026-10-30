namespace AtcSoft.VideoSurveillance.Api.Services;

public sealed class HlsStreamCommandTests
{
    private const string Playlist = @"C:\hls\cam\stream.m3u8";

    [Fact]
    public void An_Rtsp_Camera_Gets_Its_Rtsp_Transport()
    {
        var camera = CreateCamera(CoreCameraProtocol.Rtsp, 554);
        camera.Stream.RtspTransport = "udp";

        var arguments = HlsStreamCommand.BuildArguments(camera, Playlist);

        arguments.Should().Contain("-rtsp_transport udp");
        arguments.Should().Contain("-i \"rtsp://192.0.2.10:554/stream1\"");
        arguments.Should().EndWith($"\"{Playlist}\"");
    }

    [Theory]
    [InlineData(CoreCameraProtocol.Http, 80)]
    [InlineData(CoreCameraProtocol.Https, 443)]
    public void An_Http_Camera_Does_Not_Get_The_Rtsp_Only_Option(
        CoreCameraProtocol protocol,
        int port)
    {
        // FFmpeg refuses an option the input format does not know, so an
        // HTTP camera with -rtsp_transport never starts streaming.
        var camera = CreateCamera(protocol, port);

        var arguments = HlsStreamCommand.BuildArguments(camera, Playlist);

        arguments.Should().NotContain("rtsp_transport");
        arguments.Should().Contain("-c:v copy");
    }

    [Theory]
    [InlineData(CoreCameraProtocol.Https, true)]
    [InlineData(CoreCameraProtocol.Http, false)]
    [InlineData(CoreCameraProtocol.Rtsp, false)]
    public void Only_An_Https_Camera_Is_Opened_Without_Verifying_Its_Certificate(
        CoreCameraProtocol protocol,
        bool expected)
    {
        // FFmpeg 9 verifies by default; a camera's certificate is usually self-signed.
        var arguments = HlsStreamCommand.BuildArguments(CreateCamera(protocol, 443), Playlist);

        arguments.Contains("-tls_verify 0", StringComparison.Ordinal).Should().Be(expected);
    }

    [Fact]
    public void FFmpeg_Is_Told_Not_To_Print_Its_Progress()
    {
        // The progress line comes twice a second per stream, and every line
        // FFmpeg prints goes into the server's log.
        var arguments = HlsStreamCommand.BuildArguments(CreateCamera(CoreCameraProtocol.Rtsp, 554), Playlist);

        arguments.Should().StartWith("-nostats ");
    }

    [Fact]
    public void The_Password_Is_Not_In_What_Gets_Logged()
    {
        var camera = CreateCamera(CoreCameraProtocol.Rtsp, 554);
        camera.Authentication.UserName = "admin";
        camera.Authentication.Password = "s3cr3t!pw";

        var arguments = HlsStreamCommand.BuildArguments(camera, Playlist);
        arguments.Should().Contain("s3cr3t", "FFmpeg itself needs the password");

        var logged = HlsStreamCommand.Redact(arguments, camera);

        logged.Should().NotContain("s3cr3t").And.NotContain("admin");
        logged.Should().Contain("192.0.2.10:554/stream1", "the rest of the address stays readable");
    }

    [Fact]
    public void The_Password_Is_Removed_From_FFmpeg_Output_Too()
    {
        // FFmpeg prints the input address, user and password included.
        var camera = CreateCamera(CoreCameraProtocol.Rtsp, 554);
        camera.Authentication.UserName = "admin";
        camera.Authentication.Password = "p@ss word";
        var line = $"Input #0, rtsp, from '{camera.BuildUri()}':";

        var logged = HlsStreamCommand.Redact(line, camera);

        logged.Should().NotContain("p%40ss").And.NotContain("p@ss").And.NotContain("admin");
        logged.Should().StartWith("Input #0, rtsp, from 'rtsp://");
    }

    [Fact]
    public void A_Short_Password_Is_Removed_Where_It_Is_A_Password_And_Nowhere_Else()
    {
        // Removing every "1" from a line would ruin it without hiding anything.
        var camera = CreateCamera(CoreCameraProtocol.Rtsp, 554);
        camera.Authentication.UserName = "admin";
        camera.Authentication.Password = "1";
        var line = $"Input #0, rtsp, from '{camera.BuildUri()}': frame=  120 fps= 15";

        var logged = HlsStreamCommand.Redact(line, camera);

        logged.Should().NotContain("admin:1@");
        logged.Should().Contain("192.0.2.10:554/stream1").And.EndWith("frame=  120 fps= 15");
    }

    [Fact]
    public void Text_Without_Credentials_Is_Left_As_It_Is()
    {
        var camera = CreateCamera(CoreCameraProtocol.Rtsp, 554);

        HlsStreamCommand.Redact("frame=  120 fps= 15", camera).Should().Be("frame=  120 fps= 15");
    }

    [Fact]
    public void The_Tool_Is_Taken_From_The_Folder_The_Engine_Uses()
    {
        var folder = Path.Combine(Path.GetTempPath(), "atcsoft-hls-tool-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var tool = Path.Combine(folder, OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");
            File.WriteAllText(tool, string.Empty);

            HlsStreamCommand.FindTool(folder).Should().Be(tool);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData(@"C:\no-such-folder-for-ffmpeg")]
    public void Without_The_Tool_In_That_Folder_The_Path_Is_Searched(
        string? folder)
    {
        HlsStreamCommand.FindTool(folder).Should().Be("ffmpeg");
    }

    [Fact]
    public void A_Usb_Camera_Is_Not_Streamed_By_A_Process_Of_Its_Own()
    {
        // The process would hold the camera, and Windows gives it to one
        // program at a time: nothing could record it while it is watched.
        var act = () => HlsStreamCommand.BuildArguments(CreateUsbCamera("HP 5MP Camera"), Playlist);

        act.Should().Throw<ArgumentException>().WithMessage("*shared pipeline*");
    }

    [Fact]
    public void Text_About_A_Usb_Camera_Is_Left_As_It_Is()
    {
        // A USB camera has no address and no password; asking it for one throws.
        var line = "Input #0, dshow, from 'video=HP 5MP Camera':";

        HlsStreamCommand.Redact(line, CreateUsbCamera("HP 5MP Camera")).Should().Be(line);
    }

    private static CameraConfiguration CreateUsbCamera(string friendlyName)
    {
        var camera = new CameraConfiguration { Id = Guid.NewGuid() };
        camera.Connection.Source = CoreCameraSource.Usb;
        camera.Connection.Usb = new UsbConnectionSettings
        {
            DeviceId = @"\\?\usb#vid_0000&pid_0000#test",
            FriendlyName = friendlyName,
        };
        return camera;
    }

    private static CameraConfiguration CreateCamera(
        CoreCameraProtocol protocol,
        int port)
    {
        var camera = new CameraConfiguration { Id = Guid.NewGuid() };
        camera.Connection.Protocol = protocol;
        camera.Connection.IpAddress = "192.0.2.10";
        camera.Connection.Port = port;
        camera.Connection.Path = "stream1";
        return camera;
    }
}