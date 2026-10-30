namespace AtcSoft.VideoSurveillance.Models.Settings;

public class ConnectionSettingsTests
{
    [Fact]
    public void Source_Defaults_To_Network()
    {
        new ConnectionSettings().Source.Should().Be(CameraSource.Network);
    }

    [Fact]
    public void Usb_Defaults_To_Null()
    {
        new ConnectionSettings().Usb.Should().BeNull();
    }

    [Fact]
    public void Clone_Preserves_Source_And_Deep_Copies_Usb()
    {
        var original = new ConnectionSettings
        {
            Source = CameraSource.Usb,
            Usb = new UsbConnectionSettings { DeviceId = "x", FriendlyName = "Cam" },
        };

        var clone = original.Clone();
        clone.Usb!.DeviceId = "y";

        original.Usb!.DeviceId.Should().Be("x");
        clone.Source.Should().Be(CameraSource.Usb);
    }

    [Fact]
    public void CopyFrom_Replaces_Source_And_Usb()
    {
        var target = new ConnectionSettings { IpAddress = "192.168.1.1" };
        var source = new ConnectionSettings
        {
            Source = CameraSource.Usb,
            Usb = new UsbConnectionSettings { DeviceId = "x" },
        };

        target.CopyFrom(source);

        target.Source.Should().Be(CameraSource.Usb);
        target.Usb!.DeviceId.Should().Be("x");
    }

    [Fact]
    public void ValueEquals_Returns_False_When_Source_Differs()
    {
        var network = new ConnectionSettings { Source = CameraSource.Network };
        var usb = new ConnectionSettings { Source = CameraSource.Usb };

        network.ValueEquals(usb).Should().BeFalse();
    }

    [Fact]
    public void ValueEquals_Returns_True_For_Same_Network_Settings()
    {
        var a = new ConnectionSettings { IpAddress = "10.0.0.1", Port = 554, Protocol = CameraProtocol.Rtsp };
        var b = new ConnectionSettings { IpAddress = "10.0.0.1", Port = 554, Protocol = CameraProtocol.Rtsp };

        a.ValueEquals(b).Should().BeTrue();
    }

    [Fact]
    public void ValueEquals_Returns_True_For_Same_Usb_Settings()
    {
        var a = new ConnectionSettings
        {
            Source = CameraSource.Usb,
            Usb = new UsbConnectionSettings { DeviceId = "x", FriendlyName = "Cam" },
        };
        var b = new ConnectionSettings
        {
            Source = CameraSource.Usb,
            Usb = new UsbConnectionSettings { DeviceId = "x", FriendlyName = "Cam" },
        };

        a.ValueEquals(b).Should().BeTrue();
    }

    [Fact]
    public void ToString_Network_Mentions_IpAddress()
    {
        var settings = new ConnectionSettings { IpAddress = "192.168.1.1", Port = 554 };

        settings.ToString().Should().Contain("192.168.1.1").And.Contain("Network");
    }

    [Fact]
    public void ToString_Usb_Mentions_FriendlyName()
    {
        var settings = new ConnectionSettings
        {
            Source = CameraSource.Usb,
            Usb = new UsbConnectionSettings { DeviceId = "x", FriendlyName = "Logitech BRIO" },
        };

        settings.ToString().Should().Contain("Usb").And.Contain("Logitech BRIO");
    }

    [Fact]
    public void ConnectionType_Defaults_To_None()
    {
        new ConnectionSettings().ConnectionType.Should().Be(CameraConnectionType.None);
    }

    [Fact]
    public void ConnectionType_Missing_From_Json_Deserializes_To_None()
    {
        const string json = """{"Source":0,"IpAddress":"10.0.0.5","Protocol":0,"Port":554}""";

        var settings = JsonSerializer.Deserialize<ConnectionSettings>(json)!;

        settings.ConnectionType.Should().Be(CameraConnectionType.None);
    }

    [Fact]
    public void ConnectionType_Roundtrips_Through_Json()
    {
        var original = new ConnectionSettings { IpAddress = "10.0.0.5", ConnectionType = CameraConnectionType.Wifi };

        var parsed = JsonSerializer.Deserialize<ConnectionSettings>(JsonSerializer.Serialize(original))!;

        parsed.ConnectionType.Should().Be(CameraConnectionType.Wifi);
    }

    [Fact]
    public void Clone_And_CopyFrom_Preserve_ConnectionType()
    {
        var original = new ConnectionSettings { ConnectionType = CameraConnectionType.Lan };
        var target = new ConnectionSettings();

        target.CopyFrom(original);

        original.Clone().ConnectionType.Should().Be(CameraConnectionType.Lan);
        target.ConnectionType.Should().Be(CameraConnectionType.Lan);
    }

    [Fact]
    public void ValueEquals_Returns_False_When_ConnectionType_Differs()
    {
        var a = new ConnectionSettings { IpAddress = "10.0.0.5", ConnectionType = CameraConnectionType.Lan };
        var b = new ConnectionSettings { IpAddress = "10.0.0.5", ConnectionType = CameraConnectionType.Wifi };

        a.ValueEquals(b).Should().BeFalse();
    }
}