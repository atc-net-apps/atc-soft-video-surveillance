namespace AtcSoft.VideoSurveillance.Wpf.Core.Dialogs;

using CameraConfiguration = AtcSoft.VideoSurveillance.Wpf.Core.Models.CameraConfiguration;
using IWpfCoreSettingsService = AtcSoft.VideoSurveillance.Wpf.Core.Services.IApplicationSettingsService;

public class CameraConfigurationDialogViewModelUsbPickerTests
{
    [Fact]
    public void UsbDevices_StartsEmpty_ForNetworkCamera()
    {
        var enumerator = StubEnumerator(new UsbDeviceDescriptor("a", "Cam A"));

        var vm = BuildVm(BuildNetworkCamera(), enumerator);

        // Network cameras don't auto-refresh, so the list stays empty
        // until the operator switches to USB or hits Refresh.
        vm.UsbDevices.Should().BeEmpty();
    }

    [Fact]
    public void UsbDevices_AutoPopulates_WhenLoadedAsUsbCamera()
    {
        var enumerator = StubEnumerator(
            new UsbDeviceDescriptor("a", "Cam A"),
            new UsbDeviceDescriptor("b", "Cam B"));

        var vm = BuildVm(BuildUsbCamera(), enumerator);

        vm.UsbDevices.Should().HaveCount(2);
    }

    [Fact]
    public void RefreshUsbDevices_RepopulatesFromEnumerator()
    {
        var enumerator = Substitute.For<IUsbCameraEnumerator>();
        var devices = new List<UsbDeviceDescriptor> { new("a", "Cam A") };
        enumerator.EnumerateDevices(Arg.Any<CancellationToken>()).Returns(_ => devices);

        var vm = BuildVm(BuildNetworkCamera(), enumerator);
        vm.UsbDevices.Should().BeEmpty();

        // Add a device on the host between the two refreshes.
        devices.Add(new UsbDeviceDescriptor("b", "Cam B"));
        vm.RefreshUsbDevicesCommand.Execute(null);

        vm.UsbDevices.Should().HaveCount(2);
    }

    [Fact]
    public void SelectedUsbDevice_Resolves_FromCameraDeviceId()
    {
        var enumerator = StubEnumerator(
            new UsbDeviceDescriptor("a", "Cam A"),
            new UsbDeviceDescriptor("b", "Cam B"));

        var camera = BuildUsbCamera();
        camera.Connection.Usb = new UsbConnectionSettings { DeviceId = "b" };
        var vm = BuildVm(camera, enumerator);

        vm.SelectedUsbDevice.Should().NotBeNull();
        vm.SelectedUsbDevice!.DeviceId.Should().Be("b");
    }

    [Fact]
    public void SelectedUsbDevice_WritesBackTo_CameraConfiguration()
    {
        var enumerator = StubEnumerator(new UsbDeviceDescriptor("a", "Cam A"));

        var vm = BuildVm(BuildUsbCamera(), enumerator);
        var device = vm.UsbDevices[0];

        vm.SelectedUsbDevice = device;

        vm.Camera.Connection.Usb!.DeviceId.Should().Be("a");
        vm.Camera.Connection.Usb.FriendlyName.Should().Be("Cam A");
    }

    [Fact]
    public void SelectedUsbDevice_ChangingDevice_ResetsFormatTriple()
    {
        // Format is per-device — switching cameras must drop the
        // previous capture format triple to avoid silently passing
        // an unsupported (width × height × fps × pixel-format) tuple
        // to the new device's dshow stream open.
        var enumerator = StubEnumerator(
            new UsbDeviceDescriptor("a", "Cam A"),
            new UsbDeviceDescriptor("b", "Cam B"));

        var camera = BuildUsbCamera();
        camera.Connection.Usb = new UsbConnectionSettings
        {
            DeviceId = "a",
            FriendlyName = "Cam A",
            Format = new UsbStreamFormat { Width = 1920, Height = 1080, FrameRate = 30, PixelFormat = "nv12" },
        };
        var vm = BuildVm(camera, enumerator);

        var camB = vm.UsbDevices.First(d => d.DeviceId == "b");
        vm.SelectedUsbDevice = camB;

        vm.Camera.Connection.Usb!.Format.Should().BeNull();
    }

    [Fact]
    public void UsbWidthHeightFrameRatePixelFormat_LazilyCreateFormat()
    {
        // Setters must EnsureUsbFormat so binding the format dropdown
        // before any other field doesn't NRE on Camera.Connection.Usb.Format.
        var vm = BuildVm(BuildUsbCamera(), NullUsbCameraEnumerator.Instance);

        vm.UsbWidth = 1920;
        vm.UsbHeight = 1080;
        vm.UsbFrameRate = 30;
        vm.UsbPixelFormat = "nv12";
        vm.UsbCaptureAudio = true;

        vm.Camera.Connection.Usb!.Format!.Width.Should().Be(1920);
        vm.Camera.Connection.Usb.Format.Height.Should().Be(1080);
        vm.Camera.Connection.Usb.Format.FrameRate.Should().Be(30);
        vm.Camera.Connection.Usb.Format.PixelFormat.Should().Be("nv12");
        vm.Camera.Connection.Usb.PreferAudio.Should().BeTrue();
    }

    [Fact]
    public void SelectedUsbFrameRate_IgnoresNull_FromTheComboWhenItsItemsAreReplaced()
    {
        // Picking another camera replaces the frame-rate items; the combo then
        // pushes null back. A null must not reach the double (a conversion
        // error on the combo) nor clear the chosen rate.
        var vm = BuildVm(BuildUsbCamera(), NullUsbCameraEnumerator.Instance);
        vm.UsbFrameRate = 30;

        vm.SelectedUsbFrameRate = null;

        vm.UsbFrameRate.Should().Be(30);
        vm.SelectedUsbFrameRate.Should().Be(30);
    }

    [Fact]
    public void SelectedUsbFrameRate_WritesTheRate_AndIsNullWithoutOne()
    {
        var vm = BuildVm(BuildUsbCamera(), NullUsbCameraEnumerator.Instance);
        vm.SelectedUsbFrameRate.Should().BeNull();

        vm.SelectedUsbFrameRate = 15;

        vm.UsbFrameRate.Should().Be(15);
        vm.Camera.Connection.Usb!.Format!.FrameRate.Should().Be(15);
    }

    [Fact]
    public void SelectedUsbFrameRate_IsRaised_WhenTheRateChanges()
    {
        var vm = BuildVm(BuildUsbCamera(), NullUsbCameraEnumerator.Instance);
        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        vm.UsbFrameRate = 25;

        raised.Should().Contain(nameof(CameraConfigurationDialogViewModel.SelectedUsbFrameRate));
    }

    [Fact]
    public void UsbAudioDeviceName_RoundTripsToConnectionSettings()
    {
        // The dialog's audio-device text field writes directly to
        // UsbConnectionSettings.AudioDeviceName, which CameraUriHelper
        // then folds into the dshow URL when audio is enabled. Pin the
        // round-trip so a future setter rename can't silently break the
        // recording-path config.
        var vm = BuildVm(BuildUsbCamera(), NullUsbCameraEnumerator.Instance);

        vm.UsbAudioDeviceName = "Microphone (Logitech BRIO)";

        vm.Camera.Connection.Usb!.AudioDeviceName.Should().Be("Microphone (Logitech BRIO)");
    }

    [Fact]
    public void UsbAudioDeviceName_DefaultsToEmpty_OnFreshCamera()
    {
        var vm = BuildVm(BuildUsbCamera(), NullUsbCameraEnumerator.Instance);

        vm.UsbAudioDeviceName.Should().BeEmpty();
    }

    [Fact]
    public void RefreshUsbDevices_KeepsSelection_WhenDeviceStillPresent()
    {
        // A real-world Refresh shouldn't clear the user's pick if the
        // device still shows up — only when it disappears.
        var enumerator = StubEnumerator(
            new UsbDeviceDescriptor("a", "Cam A"),
            new UsbDeviceDescriptor("b", "Cam B"));

        var camera = BuildUsbCamera();
        camera.Connection.Usb = new UsbConnectionSettings { DeviceId = "a", FriendlyName = "Cam A" };
        var vm = BuildVm(camera, enumerator);

        vm.RefreshUsbDevicesCommand.Execute(null);

        vm.SelectedUsbDevice.Should().NotBeNull();
        vm.SelectedUsbDevice!.DeviceId.Should().Be("a");
    }

    [Fact]
    public void RefreshUsbDevices_ClearsSelection_WhenDeviceMissing()
    {
        var enumerator = Substitute.For<IUsbCameraEnumerator>();
        var first = new List<UsbDeviceDescriptor> { new("a", "Cam A") };
        enumerator.EnumerateDevices(Arg.Any<CancellationToken>()).Returns(_ => first);

        var camera = BuildUsbCamera();
        camera.Connection.Usb = new UsbConnectionSettings { DeviceId = "a", FriendlyName = "Cam A" };
        var vm = BuildVm(camera, enumerator);

        first.Clear();
        vm.RefreshUsbDevicesCommand.Execute(null);

        vm.SelectedUsbDevice.Should().BeNull();
    }

    [Fact]
    public void UsbResolutionItems_AreEmpty_WhenNoDeviceSelected()
    {
        var enumerator = StubEnumerator(new UsbDeviceDescriptor("a", "Cam A"));

        var vm = BuildVm(BuildUsbCamera(), enumerator);

        // No DeviceId on the camera => SelectedUsbDevice resolves to
        // null => no capabilities => empty resolution list. Otherwise
        // the combo would offer triples that won't apply to anything.
        vm.SelectedUsbDevice.Should().BeNull();
        vm.UsbResolutionItems.Should().BeEmpty();
    }

    [Fact]
    public void UsbResolutionItems_DistinctSortedByArea_FromCapabilities()
    {
        var device = new UsbDeviceDescriptor(
            "a",
            "Cam A",
            vendorId: null,
            productId: null,
            isPresent: true,
            capabilities:
            [
                new UsbStreamFormat { Width = 640, Height = 480, FrameRate = 30, PixelFormat = "nv12" },
                new UsbStreamFormat { Width = 1920, Height = 1080, FrameRate = 30, PixelFormat = "nv12" },
                new UsbStreamFormat { Width = 1920, Height = 1080, FrameRate = 60, PixelFormat = "mjpeg" }, // duplicate dim
                new UsbStreamFormat { Width = 1280, Height = 720, FrameRate = 30, PixelFormat = "nv12" },
            ]);
        var enumerator = StubEnumerator(device);

        var camera = BuildUsbCamera();
        camera.Connection.Usb = new UsbConnectionSettings { DeviceId = "a" };
        var vm = BuildVm(camera, enumerator);

        // Largest first — common UX convention for capture pickers.
        vm.UsbResolutionItems.Should().Equal(
            "1920 × 1080",
            "1280 × 720",
            "640 × 480");
    }

    [Fact]
    public void UsbFrameRateItems_FilterByCurrentResolution()
    {
        var device = new UsbDeviceDescriptor(
            "a",
            "Cam A",
            vendorId: null,
            productId: null,
            isPresent: true,
            capabilities:
            [
                new UsbStreamFormat { Width = 1920, Height = 1080, FrameRate = 30, PixelFormat = "nv12" },
                new UsbStreamFormat { Width = 1920, Height = 1080, FrameRate = 60, PixelFormat = "mjpeg" },
                new UsbStreamFormat { Width = 1280, Height = 720, FrameRate = 120, PixelFormat = "nv12" },
            ]);
        var enumerator = StubEnumerator(device);

        var camera = BuildUsbCamera();
        camera.Connection.Usb = new UsbConnectionSettings { DeviceId = "a" };
        var vm = BuildVm(camera, enumerator);

        vm.SelectedUsbResolution = "1920 × 1080";

        // Only the two frame rates the device advertises at 1080p,
        // descending. 720p's 120 fps must not bleed through.
        vm.UsbFrameRateItems.Should().Equal(60d, 30d);
    }

    [Fact]
    public void UsbPixelFormatItems_FilterByResolutionAndFrameRate()
    {
        var device = new UsbDeviceDescriptor(
            "a",
            "Cam A",
            vendorId: null,
            productId: null,
            isPresent: true,
            capabilities:
            [
                new UsbStreamFormat { Width = 1920, Height = 1080, FrameRate = 30, PixelFormat = "nv12" },
                new UsbStreamFormat { Width = 1920, Height = 1080, FrameRate = 30, PixelFormat = "yuyv422" },
                new UsbStreamFormat { Width = 1920, Height = 1080, FrameRate = 60, PixelFormat = "mjpeg" },
            ]);
        var enumerator = StubEnumerator(device);

        var camera = BuildUsbCamera();
        camera.Connection.Usb = new UsbConnectionSettings { DeviceId = "a" };
        var vm = BuildVm(camera, enumerator);

        vm.SelectedUsbResolution = "1920 × 1080";
        vm.UsbFrameRate = 30;

        vm.UsbPixelFormatItems.Should().Equal("nv12", "yuyv422");
    }

    [Fact]
    public void SelectedUsbResolution_WritesWidthAndHeight_ToFormat()
    {
        var device = new UsbDeviceDescriptor(
            "a",
            "Cam A",
            vendorId: null,
            productId: null,
            isPresent: true,
            capabilities:
            [
                new UsbStreamFormat { Width = 1920, Height = 1080, FrameRate = 30, PixelFormat = "nv12" },
            ]);
        var enumerator = StubEnumerator(device);

        var camera = BuildUsbCamera();
        camera.Connection.Usb = new UsbConnectionSettings { DeviceId = "a" };
        var vm = BuildVm(camera, enumerator);

        vm.SelectedUsbResolution = "1920 × 1080";

        vm.UsbWidth.Should().Be(1920);
        vm.UsbHeight.Should().Be(1080);
    }

    [Fact]
    public void ChangingSelectedUsbDevice_Resets_CascadingItems()
    {
        // Picking a different camera must invalidate downstream lists
        // — a cap from the old device must not linger in the combos.
        var devA = new UsbDeviceDescriptor(
            "a",
            "Cam A",
            vendorId: null,
            productId: null,
            isPresent: true,
            capabilities:
            [
                new UsbStreamFormat { Width = 1920, Height = 1080, FrameRate = 30, PixelFormat = "nv12" },
            ]);
        var devB = new UsbDeviceDescriptor(
            "b",
            "Cam B",
            vendorId: null,
            productId: null,
            isPresent: true,
            capabilities:
            [
                new UsbStreamFormat { Width = 640, Height = 480, FrameRate = 15, PixelFormat = "yuyv422" },
            ]);
        var enumerator = StubEnumerator(devA, devB);

        var camera = BuildUsbCamera();
        camera.Connection.Usb = new UsbConnectionSettings { DeviceId = "a" };
        var vm = BuildVm(camera, enumerator);

        vm.UsbResolutionItems.Should().ContainSingle().Which.Should().Be("1920 × 1080");

        vm.SelectedUsbDevice = vm.UsbDevices.First(d => d.DeviceId == "b");

        vm.UsbResolutionItems.Should().ContainSingle().Which.Should().Be("640 × 480");
    }

    private static IUsbCameraEnumerator StubEnumerator(
        params UsbDeviceDescriptor[] devices)
    {
        var enumerator = Substitute.For<IUsbCameraEnumerator>();
        enumerator.EnumerateDevices(Arg.Any<CancellationToken>()).Returns(devices);
        return enumerator;
    }

    private static CameraConfigurationDialogViewModel BuildVm(
        CameraConfiguration camera,
        IUsbCameraEnumerator enumerator)
    {
        var settingsService = Substitute.For<IWpfCoreSettingsService>();
        settingsService.CameraDisplay.Returns(new CameraDisplayAppSettings());
        settingsService.Connection.Returns(new ConnectionAppSettings());
        settingsService.Recording.Returns(new RecordingSettings());
        settingsService.Performance.Returns(new PerformanceSettings());
        settingsService.MotionDetection.Returns(new MotionDetectionSettings());

        var playerFactory = Substitute.For<IVideoPlayerFactory>();

        return new CameraConfigurationDialogViewModel(
            camera,
            isNew: true,
            existingEndpoints: [],
            settingsService,
            playerFactory,
            enumerator);
    }

    private static CameraConfiguration BuildNetworkCamera()
    {
        var camera = new CameraConfiguration();
        camera.Connection.Source = CameraSource.Network;
        return camera;
    }

    private static CameraConfiguration BuildUsbCamera()
    {
        var camera = new CameraConfiguration();
        camera.Connection.Source = CameraSource.Usb;
        camera.Connection.Usb = new UsbConnectionSettings();
        return camera;
    }
}