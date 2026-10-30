namespace AtcSoft.VideoSurveillance.Helpers;

public class CameraUriHelperTests
{
    [Fact]
    public void BuildUri_WithCameraConfiguration_Returns_Valid_Uri()
    {
        // Arrange
        var camera = new CameraConfiguration();
        camera.Connection.IpAddress = "192.168.1.10";
        camera.Connection.Port = 554;
        camera.Connection.Protocol = CameraProtocol.Rtsp;
        camera.Connection.Path = "stream1";

        // Act
        var uri = CameraUriHelper.BuildUri(camera);

        // Assert
        uri.ToString().Should().Be("rtsp://192.168.1.10:554/stream1");
    }

    [Fact]
    public void BuildUri_WithCameraConfiguration_Null_Throws()
    {
        // Act
        var act = () => CameraUriHelper.BuildUri((CameraConfiguration)null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void BuildUri_FromComponents_Returns_Valid_Rtsp_Uri()
    {
        // Act
        var uri = CameraUriHelper.BuildUri(
            CameraProtocol.Rtsp,
            "192.168.1.10",
            554,
            "stream1");

        // Assert
        uri.ToString().Should().Be("rtsp://192.168.1.10:554/stream1");
    }

    [Fact]
    public void BuildUri_FromComponents_Http_Returns_Valid_Uri()
    {
        // Act
        var uri = CameraUriHelper.BuildUri(
            CameraProtocol.Http,
            "10.0.0.1",
            80);

        // Assert
        uri.Scheme.Should().Be("http");
        uri.Host.Should().Be("10.0.0.1");
        uri.Port.Should().Be(80);
    }

    [Fact]
    public void BuildUri_FromComponents_WithCredentials_Includes_UserInfo()
    {
        // Act
        var uri = CameraUriHelper.BuildUri(
            CameraProtocol.Rtsp,
            "192.168.1.10",
            554,
            "stream1",
            "admin",
            "password");

        // Assert
        uri.ToString().Should().Contain("admin:password@");
        uri.ToString().Should().StartWith("rtsp://");
    }

    [Fact]
    public void BuildUri_FromComponents_WithoutPath_Omits_Path()
    {
        // Act
        var uri = CameraUriHelper.BuildUri(
            CameraProtocol.Rtsp,
            "192.168.1.10",
            554);

        // Assert
        uri.Host.Should().Be("192.168.1.10");
        uri.Port.Should().Be(554);
    }

    [Fact]
    public void BuildUri_FromComponents_WithLeadingSlashPath_Normalizes()
    {
        // Act
        var uri = CameraUriHelper.BuildUri(
            CameraProtocol.Rtsp,
            "192.168.1.10",
            554,
            "/stream1");

        // Assert
        uri.AbsolutePath.Should().Be("/stream1");
    }

    [Theory]
    [InlineData(CameraProtocol.Rtsp, 554)]
    [InlineData(CameraProtocol.Http, 80)]
    [InlineData(CameraProtocol.Https, 443)]
    public void GetDefaultPort_Returns_Expected_Port(
        CameraProtocol protocol,
        int expectedPort)
    {
        // Act
        var port = CameraUriHelper.GetDefaultPort(protocol);

        // Assert
        port.Should().Be(expectedPort);
    }

    [Fact]
    public void GetDefaultPort_UnknownProtocol_Returns_554()
    {
        // Arrange
        var unknown = (CameraProtocol)999;

        // Act
        var port = CameraUriHelper.GetDefaultPort(unknown);

        // Assert
        port.Should().Be(554);
    }

    [Theory]
    [InlineData(CameraSource.Network, 554)]
    [InlineData(CameraSource.Usb, 0)]
    public void GetDefaultPort_BySource_Returns_Expected(
        CameraSource source,
        int expected)
    {
        CameraUriHelper.GetDefaultPort(source).Should().Be(expected);
    }

    [Fact]
    public void BuildUri_UsbCamera_Throws()
    {
        var camera = new CameraConfiguration
        {
            Connection = { Source = CameraSource.Usb, Usb = new UsbConnectionSettings { FriendlyName = "Cam" } },
        };

        var act = () => CameraUriHelper.BuildUri(camera);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void BuildSourceLocator_NetworkCamera_ReturnsRtspUri()
    {
        var camera = new CameraConfiguration();
        camera.Connection.IpAddress = "192.168.1.10";
        camera.Connection.Port = 554;
        camera.Connection.Path = "stream1";

        var locator = CameraUriHelper.BuildSourceLocator(camera);

        locator.Uri.ToString().Should().Be("rtsp://192.168.1.10:554/stream1");
        locator.IsLocalDevice.Should().BeFalse();
        locator.InputFormat.Should().BeNull();
    }

    [Fact]
    public void BuildSourceLocator_UsbCamera_ReturnsDshowLocator_WithFormatOptions()
    {
        var camera = new CameraConfiguration
        {
            Connection =
            {
                Source = CameraSource.Usb,
                Usb = new UsbConnectionSettings
                {
                    DeviceId = @"\\?\usb#vid_046d&pid_085e",
                    FriendlyName = "Logitech BRIO",
                    Format = new UsbStreamFormat
                    {
                        Width = 1920,
                        Height = 1080,
                        FrameRate = 30,
                        PixelFormat = "nv12",
                    },
                },
            },
        };

        var locator = CameraUriHelper.BuildSourceLocator(camera);

        locator.IsLocalDevice.Should().BeTrue();
        locator.InputFormat.Should().Be("dshow");
        locator.RawDeviceSpec.Should().Be("video=Logitech BRIO");
        locator.VideoSize.Should().Be("1920x1080");
        locator.FrameRate.Should().Be("30");

        // Pixel format is intentionally NOT forwarded to dshow even
        // when the camera config carries one — see the explanatory
        // comment in CameraUriHelper.BuildUsbLocator. The saved value
        // remains on the model for UI / forensic use.
        locator.PixelFormat.Should().BeNull();
        locator.Uri.Scheme.Should().Be("dshow");
    }

    [Fact]
    public void BuildSourceLocator_UsbCamera_WithoutFormat_OmitsFormatOptions()
    {
        var camera = new CameraConfiguration
        {
            Connection =
            {
                Source = CameraSource.Usb,
                Usb = new UsbConnectionSettings { FriendlyName = "Cam" },
            },
        };

        var locator = CameraUriHelper.BuildSourceLocator(camera);

        locator.VideoSize.Should().BeNull();
        locator.FrameRate.Should().BeNull();
        locator.PixelFormat.Should().BeNull();
    }

    [Fact]
    public void BuildSourceLocator_UsbCamera_FractionalFrameRate_FormatsInvariant()
    {
        var camera = new CameraConfiguration
        {
            Connection =
            {
                Source = CameraSource.Usb,
                Usb = new UsbConnectionSettings
                {
                    FriendlyName = "Cam",
                    Format = new UsbStreamFormat { Width = 1920, Height = 1080, FrameRate = 29.97 },
                },
            },
        };

        var locator = CameraUriHelper.BuildSourceLocator(camera);

        locator.FrameRate.Should().Be("29.97");
    }

    [Fact]
    public void BuildSourceLocator_UsbCamera_FallsBackToDeviceId_WhenFriendlyNameMissing()
    {
        var camera = new CameraConfiguration
        {
            Connection =
            {
                Source = CameraSource.Usb,
                Usb = new UsbConnectionSettings { DeviceId = @"\\?\usb#abc" },
            },
        };

        var locator = CameraUriHelper.BuildSourceLocator(camera);

        locator.RawDeviceSpec.Should().Be(@"video=\\?\usb#abc");
    }

    [Fact]
    public void BuildSourceLocator_UsbCamera_NoIdentity_Throws()
    {
        var camera = new CameraConfiguration
        {
            Connection = { Source = CameraSource.Usb, Usb = new UsbConnectionSettings() },
        };

        var act = () => CameraUriHelper.BuildSourceLocator(camera);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void BuildSourceLocator_UsbCamera_MissingUsbSettings_Throws()
    {
        var camera = new CameraConfiguration
        {
            Connection = { Source = CameraSource.Usb, Usb = null },
        };

        var act = () => CameraUriHelper.BuildSourceLocator(camera);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void BuildSourceLocator_UsbCamera_AudioOptIn_AppendsCompanionAudioDevice()
    {
        // FFmpeg's dshow demuxer accepts a combined `video=X:audio=Y`
        // spec to open the camera and its microphone in one open call —
        // the right shape for capturing UVC cameras that expose an
        // integrated mic. Both the opt-in flag and a non-empty device
        // name are required (see the next two tests).
        var camera = new CameraConfiguration
        {
            Connection =
            {
                Source = CameraSource.Usb,
                Usb = new UsbConnectionSettings
                {
                    FriendlyName = "Logitech BRIO",
                    PreferAudio = true,
                    AudioDeviceName = "Microphone (Logitech BRIO)",
                },
            },
        };

        var locator = CameraUriHelper.BuildSourceLocator(camera);

        locator.RawDeviceSpec.Should().Be("video=Logitech BRIO:audio=Microphone (Logitech BRIO)");
        locator.IncludesAudio.Should().BeTrue("the recording is then made with sound");
    }

    [Fact]
    public void BuildSourceLocator_UsbCamera_AudioOptOut_OmitsAudio()
    {
        // PreferAudio = false should suppress the audio half even if a
        // device name is sitting in the model — the dialog can keep the
        // value while the operator toggles audio off without losing it
        // (matches the reasoning in UsbConnectionSettings.PreferAudio).
        var camera = new CameraConfiguration
        {
            Connection =
            {
                Source = CameraSource.Usb,
                Usb = new UsbConnectionSettings
                {
                    FriendlyName = "Logitech BRIO",
                    PreferAudio = false,
                    AudioDeviceName = "Microphone (Logitech BRIO)",
                },
            },
        };

        var locator = CameraUriHelper.BuildSourceLocator(camera);

        locator.RawDeviceSpec.Should().Be("video=Logitech BRIO");
    }

    [Fact]
    public void BuildSourceLocator_UsbCamera_PreferAudioWithoutName_OmitsAudio()
    {
        // PreferAudio without a device name is meaningless — we don't
        // blindly grab the first audio endpoint on the host. This pins
        // the safe fallback so a half-configured camera still opens.
        var camera = new CameraConfiguration
        {
            Connection =
            {
                Source = CameraSource.Usb,
                Usb = new UsbConnectionSettings
                {
                    FriendlyName = "Logitech BRIO",
                    PreferAudio = true,
                    AudioDeviceName = string.Empty,
                },
            },
        };

        var locator = CameraUriHelper.BuildSourceLocator(camera);

        locator.RawDeviceSpec.Should().Be("video=Logitech BRIO");
    }
}