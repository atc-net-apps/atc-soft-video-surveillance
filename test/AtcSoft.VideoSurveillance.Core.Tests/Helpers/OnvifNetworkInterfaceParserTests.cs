namespace AtcSoft.VideoSurveillance.Helpers;

public class OnvifNetworkInterfaceParserTests
{
    private const string Envelope = """
        <?xml version="1.0" encoding="UTF-8"?>
        <SOAP-ENV:Envelope xmlns:SOAP-ENV="http://www.w3.org/2003/05/soap-envelope"
                           xmlns:tds="http://www.onvif.org/ver10/device/wsdl"
                           xmlns:tt="http://www.onvif.org/ver10/schema">
          <SOAP-ENV:Body>
            <tds:GetNetworkInterfacesResponse>
              {0}
            </tds:GetNetworkInterfacesResponse>
          </SOAP-ENV:Body>
        </SOAP-ENV:Envelope>
        """;

    [Fact]
    public void Parse_Wired_InterfaceType_Returns_Lan()
    {
        var xml = Wrap(Interface("eth0", "192.168.1.10", interfaceType: 6));

        OnvifNetworkInterfaceParser.Parse(xml, "192.168.1.10").Should().Be(CameraConnectionType.Lan);
    }

    [Fact]
    public void Parse_Wireless_InterfaceType_Returns_Wifi()
    {
        var xml = Wrap(Interface("wlan0", "192.168.1.11", interfaceType: 71));

        OnvifNetworkInterfaceParser.Parse(xml, "192.168.1.11").Should().Be(CameraConnectionType.Wifi);
    }

    [Fact]
    public void Parse_Dot11_Configuration_Returns_Wifi()
    {
        var xml = Wrap(Interface("ra0", "192.168.1.12", interfaceType: null, dot11: true));

        OnvifNetworkInterfaceParser.Parse(xml, "192.168.1.12").Should().Be(CameraConnectionType.Wifi);
    }

    [Fact]
    public void Parse_Multiple_Interfaces_Picks_The_One_Matching_The_Ip()
    {
        var xml = Wrap(
            Interface("eth0", "192.168.1.20", interfaceType: 6) +
            Interface("wlan0", "192.168.1.21", interfaceType: 71));

        OnvifNetworkInterfaceParser.Parse(xml, "192.168.1.21").Should().Be(CameraConnectionType.Wifi);
        OnvifNetworkInterfaceParser.Parse(xml, "192.168.1.20").Should().Be(CameraConnectionType.Lan);
    }

    [Fact]
    public void Parse_Multiple_Enabled_Interfaces_Without_Ip_Match_And_Different_Types_Returns_None()
    {
        var xml = Wrap(
            Interface("eth0", "192.168.1.20", interfaceType: 6) +
            Interface("wlan0", "192.168.1.21", interfaceType: 71));

        OnvifNetworkInterfaceParser.Parse(xml, "10.0.0.1").Should().Be(CameraConnectionType.None);
    }

    [Fact]
    public void Parse_Single_Enabled_Interface_Is_Used_When_Ip_Does_Not_Match()
    {
        // e.g. the camera is reached through NAT / a DHCP address change.
        var xml = Wrap(
            Interface("eth0", "192.168.1.20", interfaceType: 6) +
            Interface("wlan0", "0.0.0.0", interfaceType: 71, enabled: false));

        OnvifNetworkInterfaceParser.Parse(xml, "10.0.0.1").Should().Be(CameraConnectionType.Lan);
    }

    [Fact]
    public void Parse_Unknown_InterfaceType_Returns_None()
    {
        // No guessing from interface names: missing evidence means "not set".
        var xml = Wrap(Interface("wlan0", "192.168.1.30", interfaceType: null));

        OnvifNetworkInterfaceParser.Parse(xml, "192.168.1.30").Should().Be(CameraConnectionType.None);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not xml")]
    [InlineData("<Envelope><Body><Fault /></Body></Envelope>")]
    public void Parse_Malformed_Or_Fault_Returns_None(string xml)
    {
        OnvifNetworkInterfaceParser.Parse(xml, "192.168.1.1").Should().Be(CameraConnectionType.None);
    }

    [Fact]
    public void ParseSystemDateAndTime_Reads_Utc()
    {
        const string xml = """
            <SOAP-ENV:Envelope xmlns:SOAP-ENV="http://www.w3.org/2003/05/soap-envelope"
                               xmlns:tds="http://www.onvif.org/ver10/device/wsdl"
                               xmlns:tt="http://www.onvif.org/ver10/schema">
              <SOAP-ENV:Body>
                <tds:GetSystemDateAndTimeResponse>
                  <tds:SystemDateAndTime>
                    <tt:UTCDateTime>
                      <tt:Time><tt:Hour>13</tt:Hour><tt:Minute>5</tt:Minute><tt:Second>9</tt:Second></tt:Time>
                      <tt:Date><tt:Year>2026</tt:Year><tt:Month>9</tt:Month><tt:Day>23</tt:Day></tt:Date>
                    </tt:UTCDateTime>
                  </tds:SystemDateAndTime>
                </tds:GetSystemDateAndTimeResponse>
              </SOAP-ENV:Body>
            </SOAP-ENV:Envelope>
            """;

        OnvifNetworkInterfaceParser.ParseSystemDateAndTime(xml)
            .Should()
            .Be(new DateTimeOffset(2026, 9, 23, 13, 5, 9, TimeSpan.Zero));
    }

    [Fact]
    public void ParseSystemDateAndTime_Returns_Null_When_Missing()
    {
        OnvifNetworkInterfaceParser.ParseSystemDateAndTime("<Envelope />").Should().BeNull();
    }

    private static string Wrap(string interfaces)
        => Envelope.Replace("{0}", interfaces, StringComparison.Ordinal);

    private static string Interface(
        string name,
        string address,
        int? interfaceType,
        bool dot11 = false,
        bool enabled = true)
    {
        var extension = interfaceType is null && !dot11
            ? string.Empty
            : "<tt:Extension>" +
              (interfaceType is null ? string.Empty : $"<tt:InterfaceType>{interfaceType}</tt:InterfaceType>") +
              (dot11 ? "<tt:Dot11><tt:SSID>53534944</tt:SSID><tt:Mode>managed</tt:Mode></tt:Dot11>" : string.Empty) +
              "</tt:Extension>";

        return $"""
            <tds:NetworkInterfaces token="{name}">
              <tt:Enabled>{(enabled ? "true" : "false")}</tt:Enabled>
              <tt:Info><tt:Name>{name}</tt:Name><tt:HwAddress>00:11:22:33:44:55</tt:HwAddress><tt:MTU>1500</tt:MTU></tt:Info>
              <tt:IPv4>
                <tt:Enabled>true</tt:Enabled>
                <tt:Config>
                  <tt:Manual><tt:Address>{address}</tt:Address><tt:PrefixLength>24</tt:PrefixLength></tt:Manual>
                  <tt:DHCP>false</tt:DHCP>
                </tt:Config>
              </tt:IPv4>
              {extension}
            </tds:NetworkInterfaces>
            """;
    }
}