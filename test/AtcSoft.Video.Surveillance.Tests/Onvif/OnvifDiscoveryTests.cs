namespace AtcSoft.Video.Surveillance.Helpers;

/// <summary>
/// The probe and its answers over UDP, against a fake camera on this machine that answers the
/// way an ONVIF camera answers the multicast probe (to the address the probe came from).
/// </summary>
public sealed class OnvifDiscoveryTests
{
    [Fact]
    public async Task The_Cameras_That_Answer_The_Probe_Are_Listed()
    {
        using var camera = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var answering = AnswerOnceAsync(camera, WsDiscoveryParserTests.TwoCameras);
        var sut = new OnvifDiscovery();

        var devices = await sut.ProbeAsync(
            (IPEndPoint)camera.Client.LocalEndPoint!,
            IPAddress.Loopback,
            TimeSpan.FromSeconds(2),
            TestContext.Current.CancellationToken);

        var probe = await answering;
        probe.Should().Contain("NetworkVideoTransmitter");
        devices.Should().NotBeNull();
        devices!.Select(d => d.Address).Should().Equal("192.168.1.64", "192.168.1.65");
    }

    [Fact]
    public async Task Nobody_Answering_Is_An_Empty_List_Not_A_Failure()
    {
        using var silent = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        var sut = new OnvifDiscovery();

        var devices = await sut.ProbeAsync(
            (IPEndPoint)silent.Client.LocalEndPoint!,
            IPAddress.Loopback,
            TimeSpan.FromMilliseconds(300),
            TestContext.Current.CancellationToken);

        devices.Should().NotBeNull().And.BeEmpty();
    }

    private static async Task<string> AnswerOnceAsync(
        UdpClient camera,
        string answer)
    {
        var probe = await camera.ReceiveAsync(TestContext.Current.CancellationToken);
        await camera.SendAsync(System.Text.Encoding.UTF8.GetBytes(answer), probe.RemoteEndPoint, TestContext.Current.CancellationToken);
        return System.Text.Encoding.UTF8.GetString(probe.Buffer);
    }
}