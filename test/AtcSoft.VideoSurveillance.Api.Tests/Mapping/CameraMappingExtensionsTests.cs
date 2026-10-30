namespace AtcSoft.VideoSurveillance.Api.Mapping;

public class CameraMappingExtensionsTests
{
    [Fact]
    public void ToApiModel_MapsAllFieldsCorrectly()
    {
        // Arrange
        var core = new CameraConfiguration();
        core.Id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        core.Display.DisplayName = "Front Door";
        core.Display.Description = "Main entrance camera";
        core.Connection.IpAddress = "192.168.1.10";
        core.Connection.Port = 554;
        core.Connection.Protocol = CoreCameraProtocol.Rtsp;
        core.Connection.Path = "stream1";
        core.Authentication.UserName = "admin";

        // Act
        var api = core.ToApiModel();

        // Assert
        api.Id.Should().Be(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        api.DisplayName.Should().Be("Front Door");
        api.Description.Should().Be("Main entrance camera");
        api.IpAddress.Should().Be("192.168.1.10");
        api.Port.Should().Be(554);
        api.Protocol.Should().Be(ApiCameraProtocol.Rtsp);
        api.Path.Should().Be("stream1");
        api.Username.Should().Be("admin");
        api.ConnectionState.Should().BeNull();
        api.IsRecording.Should().BeFalse();
    }

    [Fact]
    public void ToApiModel_WithConnectionStateAndRecording_MapsOptionalFields()
    {
        // Arrange
        var core = new CameraConfiguration();
        core.Display.DisplayName = "Test";
        core.Connection.IpAddress = "10.0.0.1";

        // Act
        var api = core.ToApiModel(
            connectionState: ConnectionState.Connected,
            isRecording: true);

        // Assert
        api.ConnectionState.Should().Be(CameraConnectionState.Connected);
        api.IsRecording.Should().BeTrue();
    }

    [Fact]
    public void ToCoreModel_MapsCreateRequestToConfiguration()
    {
        // Arrange
        var request = NetworkCreateRequest(
            displayName: "New Camera",
            description: "Test description",
            ipAddress: "10.0.0.5",
            port: 8080,
            protocol: ApiCameraProtocol.Http,
            path: "/mjpeg",
            username: "user",
            password: "secret");

        // Act
        var core = request.ToCoreModel();

        // Assert
        core.Display.DisplayName.Should().Be("New Camera");
        core.Display.Description.Should().Be("Test description");
        core.Connection.IpAddress.Should().Be("10.0.0.5");
        core.Connection.Port.Should().Be(8080);
        core.Connection.Protocol.Should().Be(CoreCameraProtocol.Http);
        core.Connection.Path.Should().Be("/mjpeg");
        core.Authentication.UserName.Should().Be("user");
        core.Authentication.Password.Should().Be("secret");
        core.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void ToCoreModel_ProtocolLeftOut_IsRtsp()
    {
        // Arrange
        var request = new CreateCameraRequest(DisplayName: "Cam", IpAddress: "10.0.0.1", Port: 554);

        // Act
        var core = request.ToCoreModel();

        // Assert
        core.Connection.Protocol.Should().Be(CoreCameraProtocol.Rtsp);
    }

    [Fact]
    public void ApplyUpdate_OnlyChangesProvidedFields()
    {
        // Arrange
        var core = new CameraConfiguration();
        core.Display.DisplayName = "Original";
        core.Display.Description = "Original desc";
        core.Connection.IpAddress = "192.168.1.1";
        core.Connection.Port = 554;

        var request = NetworkUpdateRequest(displayName: "Updated");

        // Act
        core.ApplyUpdate(request);

        // Assert
        core.Display.DisplayName.Should().Be("Updated");
        core.Display.Description.Should().Be("Original desc");
        core.Connection.IpAddress.Should().Be("192.168.1.1");
        core.Connection.Port.Should().Be(554);
    }

    [Theory]
    [InlineData(CoreCameraProtocol.Rtsp, ApiCameraProtocol.Rtsp)]
    [InlineData(CoreCameraProtocol.Http, ApiCameraProtocol.Http)]
    [InlineData(CoreCameraProtocol.Https, ApiCameraProtocol.Https)]
    public void ToApiProtocol_MapsCorrectly(
        CoreCameraProtocol core,
        ApiCameraProtocol expected)
    {
        // Act & Assert
        core.ToApiProtocol().Should().Be(expected);
    }

    [Theory]
    [InlineData(ApiCameraProtocol.Rtsp, CoreCameraProtocol.Rtsp)]
    [InlineData(ApiCameraProtocol.Http, CoreCameraProtocol.Http)]
    [InlineData(ApiCameraProtocol.Https, CoreCameraProtocol.Https)]
    public void ToCoreProtocol_MapsCorrectly(
        ApiCameraProtocol api,
        CoreCameraProtocol expected)
    {
        // Act & Assert
        api.ToCoreProtocol().Should().Be(expected);
    }

    [Theory]
    [InlineData(ConnectionState.Disconnected, CameraConnectionState.Disconnected)]
    [InlineData(ConnectionState.Connecting, CameraConnectionState.Connecting)]
    [InlineData(ConnectionState.Connected, CameraConnectionState.Connected)]
    [InlineData(ConnectionState.Reconnecting, CameraConnectionState.Reconnecting)]
    [InlineData(ConnectionState.Error, CameraConnectionState.Error)]
    public void ToApiConnectionState_MapsCorrectly(
        ConnectionState core,
        CameraConnectionState expected)
    {
        // Act & Assert
        core.ToApiConnectionState().Should().Be(expected);
    }

    [Theory]
    [InlineData(RecordingState.Idle, RecordingStatusState.Idle)]
    [InlineData(RecordingState.Recording, RecordingStatusState.Recording)]
    [InlineData(RecordingState.RecordingMotion, RecordingStatusState.RecordingMotion)]
    [InlineData(RecordingState.RecordingPostMotion, RecordingStatusState.RecordingPostMotion)]
    public void ToApiRecordingState_MapsCorrectly(
        RecordingState core,
        RecordingStatusState expected)
    {
        // Act & Assert
        core.ToApiRecordingState().Should().Be(expected);
    }

    [Fact]
    public void ToApiModel_UsbCamera_MapsAllUsbFields()
    {
        var core = new CameraConfiguration();
        core.Connection.Source = CoreCameraSource.Usb;
        core.Connection.Usb = new UsbConnectionSettings
        {
            DeviceId = "abc",
            FriendlyName = "Logitech BRIO",
            PreferAudio = true,
            AudioDeviceName = "Microphone (Logitech BRIO)",
            Format = new UsbStreamFormat { Width = 1920, Height = 1080, FrameRate = 30, PixelFormat = "nv12" },
        };
        core.Display.DisplayName = "USB Cam";

        var api = core.ToApiModel();

        api.Source.Should().Be(ApiCameraSource.Usb);
        api.UsbDeviceId.Should().Be("abc");
        api.UsbFriendlyName.Should().Be("Logitech BRIO");
        api.UsbWidth.Should().Be(1920);
        api.UsbHeight.Should().Be(1080);
        api.UsbFrameRate.Should().Be(30);
        api.UsbPixelFormat.Should().Be("nv12");
        api.UsbCaptureAudio.Should().BeTrue();
        api.UsbAudioDeviceName.Should().Be("Microphone (Logitech BRIO)");
    }

    [Fact]
    public void ToApiModel_UsbCamera_WithoutAudioDevice_EmitsEmptyAudioDeviceName()
    {
        var core = new CameraConfiguration();
        core.Connection.Source = CoreCameraSource.Usb;
        core.Connection.Usb = new UsbConnectionSettings
        {
            DeviceId = "abc",
            FriendlyName = "Webcam",

            // AudioDeviceName intentionally left default to pin that
            // a USB camera with no audio device round-trips as "" not null.
        };
        core.Display.DisplayName = "USB Cam";

        var api = core.ToApiModel();

        api.UsbAudioDeviceName.Should().BeEmpty();
    }

    [Fact]
    public void ToCoreModel_UsbRequest_PopulatesUsbConnectionSettings()
    {
        var request = new CreateCameraRequest(
            DisplayName: "USB Cam",
            Description: string.Empty,
            Source: ApiCameraSource.Usb,
            ConnectionType: null,
            Rotation: null,
            IpAddress: string.Empty,
            Path: string.Empty,
            Username: string.Empty,
            Password: string.Empty,
            UsbDeviceId: "device-1",
            UsbFriendlyName: "Logitech BRIO",
            UsbWidth: 1280,
            UsbHeight: 720,
            UsbFrameRate: 60,
            UsbPixelFormat: "mjpeg",
            UsbCaptureAudio: true,
            UsbAudioDeviceName: "Microphone (Logitech BRIO)",
            OverlayPosition: null,
            StreamUseLowLatencyMode: true,
            StreamMaxLatencyMs: 500,
            StreamRtspTransport: null,
            StreamBufferDurationMs: 0,
            Port: 0);

        var core = request.ToCoreModel();

        core.Connection.Source.Should().Be(CoreCameraSource.Usb);
        core.Connection.Usb.Should().NotBeNull();
        core.Connection.Usb!.DeviceId.Should().Be("device-1");
        core.Connection.Usb.FriendlyName.Should().Be("Logitech BRIO");
        core.Connection.Usb.PreferAudio.Should().BeTrue();
        core.Connection.Usb.AudioDeviceName.Should().Be("Microphone (Logitech BRIO)");
        core.Connection.Usb.Format.Should().NotBeNull();
        core.Connection.Usb.Format!.Width.Should().Be(1280);
        core.Connection.Usb.Format.Height.Should().Be(720);
        core.Connection.Usb.Format.FrameRate.Should().Be(60);
        core.Connection.Usb.Format.PixelFormat.Should().Be("mjpeg");
    }

    [Fact]
    public void ApplyUpdate_UsbAudioDeviceName_OverwritesExistingValue()
    {
        // Existing USB camera with an audio device set; an update that
        // changes only the audio device name must take effect and not
        // require resetting the entire format block.
        var core = new CameraConfiguration();
        core.Connection.Source = CoreCameraSource.Usb;
        core.Connection.Usb = new UsbConnectionSettings
        {
            DeviceId = "device-1",
            FriendlyName = "Logitech BRIO",
            PreferAudio = true,
            AudioDeviceName = "Old microphone",
        };

        var request = new UpdateCameraRequest(
            DisplayName: null!,
            Description: null!,
            Source: null,
            ConnectionType: null,
            Rotation: null,
            IpAddress: null!,
            Port: 0,
            Protocol: null,
            Path: null!,
            Username: null!,
            Password: null!,
            UsbDeviceId: null!,
            UsbFriendlyName: null!,
            UsbWidth: 0,
            UsbHeight: 0,
            UsbFrameRate: 0,
            UsbPixelFormat: null!,
            UsbCaptureAudio: null,
            UsbAudioDeviceName: "Microphone (Logitech BRIO)",
            OverlayPosition: null,
            StreamUseLowLatencyMode: true,
            StreamMaxLatencyMs: 500,
            StreamRtspTransport: null,
            StreamBufferDurationMs: 0);

        core.ApplyUpdate(request);

        core.Connection.Usb!.AudioDeviceName.Should().Be("Microphone (Logitech BRIO)");

        // usbCaptureAudio was left out, so the sound stays on.
        core.Connection.Usb.PreferAudio.Should().BeTrue();
    }

    [Fact]
    public void ApplyUpdate_Without_Fields_Changes_Nothing()
    {
        // A client that predates a field leaves it out of its update.
        var core = new CameraConfiguration();
        core.Display.DisplayName = "Gate";
        core.Connection.Source = CoreCameraSource.Usb;
        core.Connection.ConnectionType = CoreCameraConnectionType.Usb;
        core.Connection.Usb = new UsbConnectionSettings
        {
            DeviceId = "device-1",
            FriendlyName = "Logitech BRIO",
            PreferAudio = true,
            AudioDeviceName = "Microphone (Logitech BRIO)",
            Format = new UsbStreamFormat { Width = 1280, Height = 720, FrameRate = 30, PixelFormat = "mjpeg" },
        };
        core.Stream.UseLowLatencyMode = !core.Stream.UseLowLatencyMode;
        core.Stream.MaxLatencyMs = 750;
        core.Stream.BufferDurationMs = 250;
        var before = JsonSerializer.Serialize(core);

        core.ApplyUpdate(new UpdateCameraRequest());

        JsonSerializer.Serialize(core).Should().Be(before);
    }

    [Fact]
    public void ApplyUpdate_UsbCaptureAudio_False_Turns_The_Sound_Off()
    {
        var core = new CameraConfiguration();
        core.Connection.Source = CoreCameraSource.Usb;
        core.Connection.Usb = new UsbConnectionSettings { DeviceId = "device-1", PreferAudio = true };

        core.ApplyUpdate(new UpdateCameraRequest(UsbCaptureAudio: false));

        core.Connection.Usb!.PreferAudio.Should().BeFalse();
    }

    [Fact]
    public void ToCoreModel_NetworkRequest_LeavesUsbNull()
    {
        var request = NetworkCreateRequest(
            displayName: "Net Cam",
            ipAddress: "10.0.0.1",
            port: 554,
            protocol: ApiCameraProtocol.Rtsp);

        var core = request.ToCoreModel();

        core.Connection.Source.Should().Be(CoreCameraSource.Network);
        core.Connection.Usb.Should().BeNull();
    }

    [Fact]
    public void ToApiModel_Maps_Stored_Network_ConnectionType()
    {
        var core = new CameraConfiguration();
        core.Connection.ConnectionType = CoreCameraConnectionType.Wifi;

        core.ToApiModel().ConnectionType.Should().Be(ApiCameraConnectionType.Wifi);
    }

    [Fact]
    public void ToApiModel_Usb_Source_Is_Always_Usb()
    {
        var core = new CameraConfiguration();
        core.Connection.Source = CoreCameraSource.Usb;
        core.Connection.ConnectionType = CoreCameraConnectionType.None;

        core.ToApiModel().ConnectionType.Should().Be(ApiCameraConnectionType.Usb);
    }

    [Fact]
    public void ToCoreModel_Maps_Network_ConnectionType()
    {
        var request = NetworkCreateRequest("Cam", ipAddress: "10.0.0.1") with { ConnectionType = ApiCameraConnectionType.Lan };

        request.ToCoreModel().Connection.ConnectionType.Should().Be(CoreCameraConnectionType.Lan);
    }

    [Fact]
    public void ToCoreModel_Network_Camera_Ignores_Usb_ConnectionType()
    {
        var request = NetworkCreateRequest("Cam", ipAddress: "10.0.0.1") with { ConnectionType = ApiCameraConnectionType.Usb };

        request.ToCoreModel().Connection.ConnectionType.Should().Be(CoreCameraConnectionType.None);
    }

    [Fact]
    public void ToCoreModel_Usb_Source_Forces_Usb_ConnectionType()
    {
        var request = NetworkCreateRequest("Cam") with
        {
            Source = ApiCameraSource.Usb,
            UsbDeviceId = "dev",
            ConnectionType = ApiCameraConnectionType.Wifi,
        };

        request.ToCoreModel().Connection.ConnectionType.Should().Be(CoreCameraConnectionType.Usb);
    }

    [Theory]
    [InlineData(CoreCameraRotation.None, ApiCameraRotation.None)]
    [InlineData(CoreCameraRotation.Rotate90, ApiCameraRotation.Rotate90)]
    [InlineData(CoreCameraRotation.Rotate180, ApiCameraRotation.Rotate180)]
    [InlineData(CoreCameraRotation.Rotate270, ApiCameraRotation.Rotate270)]
    public void ToApiModel_Maps_Rotation(
        CoreCameraRotation core,
        ApiCameraRotation expected)
    {
        var camera = new CameraConfiguration();
        camera.Display.Rotation = core;

        camera.ToApiModel().Rotation.Should().Be(expected);
    }

    [Theory]
    [InlineData(ApiCameraRotation.Rotate90, CoreCameraRotation.Rotate90)]
    [InlineData(ApiCameraRotation.Rotate180, CoreCameraRotation.Rotate180)]
    [InlineData(ApiCameraRotation.Rotate270, CoreCameraRotation.Rotate270)]
    public void ToCoreModel_Maps_Rotation(
        ApiCameraRotation api,
        CoreCameraRotation expected)
    {
        var request = NetworkCreateRequest("Cam", ipAddress: "10.0.0.1") with { Rotation = api };

        request.ToCoreModel().Display.Rotation.Should().Be(expected);
    }

    [Fact]
    public void ToCoreModel_Without_Rotation_Is_Upright()
    {
        var request = NetworkCreateRequest("Cam", ipAddress: "10.0.0.1");

        request.ToCoreModel().Display.Rotation.Should().Be(CoreCameraRotation.None);
    }

    [Fact]
    public void ApplyUpdate_Sets_Rotation_When_Provided()
    {
        var core = new CameraConfiguration();
        core.Display.Rotation = CoreCameraRotation.Rotate90;

        core.ApplyUpdate(NetworkUpdateRequest() with { Rotation = ApiCameraRotation.Rotate180 });

        core.Display.Rotation.Should().Be(CoreCameraRotation.Rotate180);
    }

    [Fact]
    public void ApplyUpdate_Can_Set_Rotation_Back_To_Upright()
    {
        var core = new CameraConfiguration();
        core.Display.Rotation = CoreCameraRotation.Rotate90;

        core.ApplyUpdate(NetworkUpdateRequest() with { Rotation = ApiCameraRotation.None });

        core.Display.Rotation.Should().Be(CoreCameraRotation.None);
    }

    [Fact]
    public void ApplyUpdate_Keeps_Rotation_When_Not_Provided()
    {
        var core = new CameraConfiguration();
        core.Display.Rotation = CoreCameraRotation.Rotate270;

        core.ApplyUpdate(NetworkUpdateRequest());

        core.Display.Rotation.Should().Be(CoreCameraRotation.Rotate270);
    }

    [Fact]
    public void The_Motion_Zones_Go_Both_Ways()
    {
        List<ApiMotionZone> zones =
        [
            new("Tree", ApiMotionZoneKind.Ignore, [new ApiZonePoint(0.1, 0.2), new ApiZonePoint(0.4, 0.2), new ApiZonePoint(0.4, 0.9)], "Green"),
            new("Gate", ApiMotionZoneKind.Watch, [new ApiZonePoint(0.5, 0.5), new ApiZonePoint(0.9, 0.5), new ApiZonePoint(0.9, 0.9)], null, Sensitivity: 80, SmallestObjectPercent: 0.1),
        ];

        var core = (NetworkCreateRequest("Cam", ipAddress: "10.0.0.1") with { MotionZones = zones }).ToCoreModel();
        var api = core.ToApiModel();

        core.MotionZones.Select(z => (z.Name, z.Kind, z.Color)).Should().Equal(("Tree", CoreMotionZoneKind.Ignore, "Green"), ("Gate", CoreMotionZoneKind.Watch, (string?)null));
        core.MotionZones[0].Points.Should().Equal(new CoreZonePoint(0.1, 0.2), new CoreZonePoint(0.4, 0.2), new CoreZonePoint(0.4, 0.9));
        core.MotionZones.Select(z => (z.Sensitivity, z.SmallestObjectPercent)).Should().Equal(((int?)null, (double?)null), (80, 0.1));
        api.MotionZones.Should().BeEquivalentTo(zones);
    }

    [Fact]
    public void ApplyUpdate_Keeps_The_Zones_When_Left_Out_And_Clears_Them_When_Empty()
    {
        var core = new CameraConfiguration { MotionZones = [Triangle()] };

        core.ApplyUpdate(NetworkUpdateRequest());
        core.MotionZones.Should().HaveCount(1);

        core.ApplyUpdate(NetworkUpdateRequest() with { MotionZones = [] });
        core.MotionZones.Should().BeEmpty();
    }

    [Fact]
    public void ApplyUpdate_Turns_The_Zones_With_The_Picture()
    {
        var core = new CameraConfiguration { MotionZones = [Triangle()] };

        core.ApplyUpdate(NetworkUpdateRequest() with { Rotation = ApiCameraRotation.Rotate90 });

        core.MotionZones[0].Points[0].Should().Be(new CoreZonePoint(0.8, 0.1), "(0.1, 0.2) turned a quarter clockwise");
    }

    [Fact]
    public void ApplyUpdate_Takes_Zones_Sent_With_A_Rotation_As_Drawn()
    {
        var core = new CameraConfiguration { MotionZones = [Triangle()] };

        core.ApplyUpdate(NetworkUpdateRequest() with
        {
            Rotation = ApiCameraRotation.Rotate90,
            MotionZones = [new("Tree", ApiMotionZoneKind.Ignore, [new ApiZonePoint(0.1, 0.2), new ApiZonePoint(0.4, 0.2), new ApiZonePoint(0.4, 0.9)], null)],
        });

        core.MotionZones[0].Points[0].Should().Be(new CoreZonePoint(0.1, 0.2));
    }

    [Fact]
    public void ApplyUpdate_Sets_ConnectionType_When_Provided()
    {
        var core = new CameraConfiguration();
        core.Connection.ConnectionType = CoreCameraConnectionType.Lan;

        core.ApplyUpdate(NetworkUpdateRequest() with { ConnectionType = ApiCameraConnectionType.Wifi });

        core.Connection.ConnectionType.Should().Be(CoreCameraConnectionType.Wifi);
    }

    [Fact]
    public void ApplyUpdate_Keeps_ConnectionType_When_Not_Provided()
    {
        var core = new CameraConfiguration();
        core.Connection.ConnectionType = CoreCameraConnectionType.Lan;

        core.ApplyUpdate(NetworkUpdateRequest());

        core.Connection.ConnectionType.Should().Be(CoreCameraConnectionType.Lan);
    }

    private static CreateCameraRequest NetworkCreateRequest(
        string displayName,
        string description = "",
        string ipAddress = "",
        int port = 554,
        ApiCameraProtocol protocol = ApiCameraProtocol.Rtsp,
        string path = "",
        string username = "",
        string password = "")
        => new(
            DisplayName: displayName,
            Description: description,
            Source: ApiCameraSource.Network,
            ConnectionType: null,
            Rotation: null,
            IpAddress: ipAddress,
            Protocol: protocol,
            Path: path,
            Username: username,
            Password: password,
            UsbDeviceId: string.Empty,
            UsbFriendlyName: string.Empty,
            UsbWidth: 0,
            UsbHeight: 0,
            UsbFrameRate: 0,
            UsbPixelFormat: string.Empty,
            UsbCaptureAudio: false,
            UsbAudioDeviceName: string.Empty,
            OverlayPosition: null,
            StreamUseLowLatencyMode: true,
            StreamMaxLatencyMs: 500,
            StreamRtspTransport: null,
            StreamBufferDurationMs: 0,
            Port: port);

    // Mirrors the original "minimal update" shape from before USB
    // landed: every nullable field is null so ApplyUpdate's "only
    // overwrite when non-null" semantics keep existing values.
    private static CoreMotionZone Triangle()
        => new()
        {
            Name = "Tree",
            Kind = CoreMotionZoneKind.Ignore,
            Points = [new(0.1, 0.2), new(0.4, 0.2), new(0.4, 0.9)],
        };

    private static UpdateCameraRequest NetworkUpdateRequest(
        string displayName = "")
        => new(
            DisplayName: displayName,
            Description: null!,
            Source: null,
            ConnectionType: null,
            Rotation: null,
            IpAddress: null!,
            Port: 0,
            Protocol: null,
            Path: null!,
            Username: null!,
            Password: null!,
            UsbDeviceId: null!,
            UsbFriendlyName: null!,
            UsbWidth: 0,
            UsbHeight: 0,
            UsbFrameRate: 0,
            UsbPixelFormat: null!,
            UsbCaptureAudio: false,
            UsbAudioDeviceName: null!,
            OverlayPosition: null,
            StreamUseLowLatencyMode: true,
            StreamMaxLatencyMs: 500,
            StreamRtspTransport: null,
            StreamBufferDurationMs: 0);
}