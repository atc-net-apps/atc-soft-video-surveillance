namespace AtcSoft.VideoSurveillance.Wpf.Core.Services;

using CameraConfiguration = AtcSoft.VideoSurveillance.Wpf.Core.Models.CameraConfiguration;

public sealed class CameraStorageServiceUsbTests : IDisposable
{
    private readonly string tempPath;

    public CameraStorageServiceUsbTests()
    {
        tempPath = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"camera-storage-usb-{Guid.NewGuid():N}.json");
    }

    [Fact]
    public void RoundTrip_UsbCamera_PreservesSourceAndUsbSettings()
    {
        // The Wpf.Core wrapper layers serialization on top of Core, so a
        // round-trip through the on-disk JSON is the only place that
        // exercises the full deserializer config (camelCase + the custom
        // CameraConfigurationJsonValueConverter). Anything we silently
        // drop here would break end-users with USB cameras stored from
        // a previous session.
        var original = BuildUsbCamera(
            "Front Door Webcam",
            deviceId: @"\\?\usb#vid_046d&pid_085e",
            friendlyName: "Logitech BRIO",
            width: 1920,
            height: 1080,
            frameRate: 30,
            pixelFormat: "nv12",
            preferAudio: true);

        var writer = new CameraStorageService(tempPath);
        writer.AddOrUpdateCamera(original);

        var reader = new CameraStorageService(tempPath);
        var loaded = reader.GetAllCameras().Should().ContainSingle().Subject;

        loaded.Connection.Source.Should().Be(CameraSource.Usb);
        loaded.Connection.Usb.Should().NotBeNull();
        loaded.Connection.Usb!.DeviceId.Should().Be(@"\\?\usb#vid_046d&pid_085e");
        loaded.Connection.Usb.FriendlyName.Should().Be("Logitech BRIO");
        loaded.Connection.Usb.PreferAudio.Should().BeTrue();
        loaded.Connection.Usb.Format.Should().NotBeNull();
        loaded.Connection.Usb.Format!.Width.Should().Be(1920);
        loaded.Connection.Usb.Format.Height.Should().Be(1080);
        loaded.Connection.Usb.Format.FrameRate.Should().Be(30);
        loaded.Connection.Usb.Format.PixelFormat.Should().Be("nv12");
    }

    [Fact]
    public void RoundTrip_NetworkCamera_DefaultsSourceToNetwork()
    {
        // Belt-and-suspenders: even though Source = Network is the
        // ctor default, the on-disk shape must round-trip without
        // accidentally promoting a network camera to USB.
        var original = new CameraConfiguration();
        original.Connection.IpAddress = "192.168.1.10";
        original.Connection.Port = 554;
        original.Connection.Path = "stream1";
        original.Display.DisplayName = "Front Garden";

        var writer = new CameraStorageService(tempPath);
        writer.AddOrUpdateCamera(original);

        var reader = new CameraStorageService(tempPath);
        var loaded = reader.GetAllCameras().Should().ContainSingle().Subject;

        loaded.Connection.Source.Should().Be(CameraSource.Network);
        loaded.Connection.Usb.Should().BeNull();
        loaded.Connection.IpAddress.Should().Be("192.168.1.10");
    }

    [Fact]
    public void Load_V1JsonWithoutSourceField_DefaultsToNetwork()
    {
        // v1 storage files (pre-USB) carry no `source` field. The
        // additive migration of USB cameras into existing files
        // hinges on `Source` defaulting to Network when absent — this
        // test pins that contract so a serializer change can't silently
        // break end-users on upgrade.
        const string v1Json = """
        {
          "cameras": [
            {
              "id": "11111111-1111-1111-1111-111111111111",
              "connection": {
                "ipAddress": "10.0.0.42",
                "protocol": "Rtsp",
                "port": 554,
                "path": "live"
              },
              "authentication": { "userName": "u", "password": "p" },
              "display": { "displayName": "Legacy v1 Camera" },
              "stream": {}
            }
          ],
          "layouts": []
        }
        """;
        System.IO.File.WriteAllText(tempPath, v1Json);

        var service = new CameraStorageService(tempPath);
        var loaded = service.GetAllCameras().Should().ContainSingle().Subject;

        loaded.Connection.Source.Should().Be(CameraSource.Network);
        loaded.Connection.Usb.Should().BeNull();
        loaded.Connection.IpAddress.Should().Be("10.0.0.42");
    }

    [Fact]
    public void Load_V2JsonWithUsbCamera_ReconstructsFormatTriple()
    {
        // Mirror image of the v1 test — pin the on-disk shape USB cameras
        // serialize to. Hand-written rather than generated so a converter
        // change that swaps property casing or moves Format up a level is
        // caught immediately instead of via a later UI breakage.
        const string usbJson = """
        {
          "cameras": [
            {
              "id": "22222222-2222-2222-2222-222222222222",
              "connection": {
                "source": "Usb",
                "usb": {
                  "deviceId": "\\\\?\\usb#vid_046d&pid_085e",
                  "friendlyName": "Logitech BRIO",
                  "format": {
                    "width": 1280,
                    "height": 720,
                    "frameRate": 60,
                    "pixelFormat": "mjpeg"
                  },
                  "preferAudio": false
                }
              },
              "authentication": { "userName": "", "password": "" },
              "display": { "displayName": "Office Webcam" },
              "stream": {}
            }
          ],
          "layouts": []
        }
        """;
        System.IO.File.WriteAllText(tempPath, usbJson);

        var service = new CameraStorageService(tempPath);
        var loaded = service.GetAllCameras().Should().ContainSingle().Subject;

        loaded.Connection.Source.Should().Be(CameraSource.Usb);
        loaded.Connection.Usb.Should().NotBeNull();
        loaded.Connection.Usb!.Format.Should().NotBeNull();
        loaded.Connection.Usb.Format!.Width.Should().Be(1280);
        loaded.Connection.Usb.Format.Height.Should().Be(720);
        loaded.Connection.Usb.Format.FrameRate.Should().Be(60);
        loaded.Connection.Usb.Format.PixelFormat.Should().Be("mjpeg");
    }

    public void Dispose()
    {
        if (System.IO.File.Exists(tempPath))
        {
            System.IO.File.Delete(tempPath);
        }
    }

    private static CameraConfiguration BuildUsbCamera(
        string displayName,
        string deviceId,
        string friendlyName,
        int width,
        int height,
        double frameRate,
        string pixelFormat,
        bool preferAudio)
    {
        var camera = new CameraConfiguration();
        camera.Display.DisplayName = displayName;
        camera.Connection.Source = CameraSource.Usb;
        camera.Connection.Usb = new UsbConnectionSettings
        {
            DeviceId = deviceId,
            FriendlyName = friendlyName,
            PreferAudio = preferAudio,
            Format = new UsbStreamFormat
            {
                Width = width,
                Height = height,
                FrameRate = frameRate,
                PixelFormat = pixelFormat,
            },
        };
        return camera;
    }
}