namespace AtcSoft.Video.Surveillance.Helpers;

/// <summary>
/// The address of each of a camera's streams: the sub-stream shares the main stream's host,
/// protocol and credentials, and has a path (and a port) of its own.
/// </summary>
public sealed class CameraUriHelperStreamRoleTests
{
    [Fact]
    public void The_Main_Stream_Is_The_Camera_Address()
        => CameraUriHelper.BuildSourceLocator(Camera("/stream2"), CameraStreamRole.Main).Uri
            .ToString().Should().Be("rtsp://admin:p%40ss@192.0.2.10:554/stream1");

    [Fact]
    public void The_Sub_Stream_Has_Its_Own_Path_With_The_Same_Credentials()
        => CameraUriHelper.BuildSourceLocator(Camera("/stream2"), CameraStreamRole.Sub).Uri
            .ToString().Should().Be("rtsp://admin:p%40ss@192.0.2.10:554/stream2");

    [Fact]
    public void The_Sub_Stream_Can_Have_Its_Own_Port()
        => Camera("stream2", subStreamPort: 8554).BuildUri(CameraStreamRole.Sub)
            .ToString().Should().Be("rtsp://admin:p%40ss@192.0.2.10:8554/stream2");

    [Fact]
    public void A_Camera_Without_A_Sub_Stream_Opens_The_Main_Stream_For_Either_Role()
        => Camera(subStreamPath: null).BuildUri(CameraStreamRole.Sub)
            .Should().Be(Camera(subStreamPath: null).BuildUri(CameraStreamRole.Main));

    [Fact]
    public void A_Path_With_A_Query_Keeps_It()
    {
        // Dahua's streams are chosen by query: the main stream is subtype=0, the sub-stream subtype=1.
        var camera = Camera("/cam/realmonitor?channel=1&subtype=1", mainPath: "/cam/realmonitor?channel=1&subtype=0");

        camera.BuildUri(CameraStreamRole.Main).PathAndQuery.Should().Be("/cam/realmonitor?channel=1&subtype=0");
        camera.BuildUri(CameraStreamRole.Sub).PathAndQuery.Should().Be("/cam/realmonitor?channel=1&subtype=1");
    }

    [Fact]
    public void The_Main_Stream_Is_What_A_Caller_Without_A_Role_Gets()
    {
        var camera = Camera("/stream2");

        camera.BuildUri().Should().Be(camera.BuildUri(CameraStreamRole.Main));
        CameraUriHelper.BuildSourceLocator(camera).Uri.Should().Be(camera.BuildUri(CameraStreamRole.Main));
    }

    private static CameraConfiguration Camera(
        string? subStreamPath,
        int? subStreamPort = null,
        string mainPath = "/stream1")
    {
        var camera = new CameraConfiguration();
        camera.Connection.IpAddress = "192.0.2.10";
        camera.Connection.Path = mainPath;
        camera.Connection.SubStreamPath = subStreamPath;
        camera.Connection.SubStreamPort = subStreamPort;
        camera.Authentication.UserName = "admin";
        camera.Authentication.Password = "p@ss";
        return camera;
    }
}
