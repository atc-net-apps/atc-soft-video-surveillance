namespace AtcSoft.Video.Surveillance.Helpers;

public sealed class WsDiscoveryParserTests
{
    // Two cameras in one answer, as an NVR that answers for its cameras sends it; the second
    // names a public address first, then a private one.
    internal const string TwoCameras = """
        <SOAP-ENV:Envelope xmlns:SOAP-ENV="http://www.w3.org/2003/05/soap-envelope" xmlns:wsa="http://schemas.xmlsoap.org/ws/2004/08/addressing" xmlns:d="http://schemas.xmlsoap.org/ws/2005/04/discovery">
          <SOAP-ENV:Body>
            <d:ProbeMatches>
              <d:ProbeMatch>
                <wsa:EndpointReference><wsa:Address>urn:uuid:4d454930-0000-1000-8000-bcbac2c4a8e1</wsa:Address></wsa:EndpointReference>
                <d:Types>dn:NetworkVideoTransmitter tds:Device</d:Types>
                <d:Scopes>onvif://www.onvif.org/type/video_encoder onvif://www.onvif.org/name/Front%20Door onvif://www.onvif.org/hardware/DS-2CD2143G2-I</d:Scopes>
                <d:XAddrs>http://192.168.1.64/onvif/device_service</d:XAddrs>
              </d:ProbeMatch>
              <d:ProbeMatch>
                <wsa:EndpointReference><wsa:Address>urn:uuid:a1b2c3d4-0000-1000-8000-000000000002</wsa:Address></wsa:EndpointReference>
                <d:Scopes>onvif://www.onvif.org/name/Garage_Cam onvif://www.onvif.org/hardware/C210</d:Scopes>
                <d:XAddrs>http://203.0.113.7:2020/onvif/device_service http://192.168.1.65:2020/onvif/device_service</d:XAddrs>
              </d:ProbeMatch>
            </d:ProbeMatches>
          </SOAP-ENV:Body>
        </SOAP-ENV:Envelope>
        """;

    [Fact]
    public void The_Devices_Are_Read_With_Their_Name_Model_And_Private_Address()
    {
        var devices = WsDiscoveryParser.ParseProbeMatches(TwoCameras);

        devices.Select(d => (d.Name, d.Hardware, d.Address, d.Port)).Should().Equal(
            ("Front Door", "DS-2CD2143G2-I", "192.168.1.64", 80),
            ("Garage Cam", "C210", "192.168.1.65", 2020));
    }

    [Fact]
    public void A_Device_With_Only_A_Public_Address_Is_Left_Out()
    {
        var answer = TwoCameras.Replace("http://192.168.1.65:2020/onvif/device_service", string.Empty, StringComparison.Ordinal);

        WsDiscoveryParser.ParseProbeMatches(answer).Should().ContainSingle().Which.Name.Should().Be("Front Door");
    }

    [Theory]
    [InlineData("http://10.1.2.3/x", true)]
    [InlineData("http://172.20.0.5/x", true)]
    [InlineData("http://172.32.0.5/x", false)]
    [InlineData("http://192.168.0.10/x", true)]
    [InlineData("http://169.254.1.1/x", true)]
    [InlineData("http://8.8.8.8/x", false)]
    [InlineData("http://camera.local/x", true)]
    public void Only_Local_Network_Addresses_Count(
        string address,
        bool local)
        => WsDiscoveryParser.IsOnLocalNetwork(new Uri(address)).Should().Be(local);

    [Fact]
    public void The_Probe_Asks_For_Network_Video_Transmitters()
    {
        var probe = WsDiscoveryParser.BuildProbe(Guid.Parse("11111111-2222-3333-4444-555555555555"));

        probe.Should().Contain("dn:NetworkVideoTransmitter")
            .And.Contain("uuid:11111111-2222-3333-4444-555555555555")
            .And.Contain("http://schemas.xmlsoap.org/ws/2005/04/discovery/Probe");
    }

    [Fact]
    public void An_Answer_That_Is_No_Xml_Gives_Nothing()
        => WsDiscoveryParser.ParseProbeMatches("M-SEARCH * HTTP/1.1").Should().BeEmpty();
}