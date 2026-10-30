namespace AtcSoft.VideoEngine.Windows.MediaFoundation;

public class MediaFoundationEnumeratorTests
{
    [Fact]
    public void EnumerateDevices_Empty_ReturnsEmpty()
    {
        var enumerator = new MediaFoundationEnumerator(new FakeProbe([]));

        enumerator.EnumerateDevices(TestContext.Current.CancellationToken).Should().BeEmpty();
    }

    [Fact]
    public void EnumerateDevices_MapsRowsToDescriptors_AndExtractsVidPid()
    {
        var probe = new FakeProbe(
        [
            new MfDeviceRow(@"\\?\usb#vid_046d&pid_085e&mi_00#abc", "Logitech BRIO", []),
            new MfDeviceRow(@"\\?\usb#vid_05ac&pid_8514", "Apple FaceTime HD", []),
        ]);

        var enumerator = new MediaFoundationEnumerator(probe);

        var devices = enumerator.EnumerateDevices(TestContext.Current.CancellationToken);

        devices.Should().HaveCount(2);

        // Sorted alphabetically by friendly name.
        devices[0].FriendlyName.Should().Be("Apple FaceTime HD");
        devices[0].VendorId.Should().Be("05ac");
        devices[0].ProductId.Should().Be("8514");

        devices[1].FriendlyName.Should().Be("Logitech BRIO");
        devices[1].VendorId.Should().Be("046d");
        devices[1].ProductId.Should().Be("085e");

        devices[0].IsPresent.Should().BeTrue();
    }

    [Fact]
    public void FindByDeviceId_ReturnsMatchingDescriptor()
    {
        var probe = new FakeProbe(
        [
            new MfDeviceRow(@"\\?\usb#vid_046d&pid_085e", "Logitech BRIO", []),
        ]);

        var enumerator = new MediaFoundationEnumerator(probe);

        var found = enumerator.FindByDeviceId(@"\\?\usb#vid_046d&pid_085e");

        found.Should().NotBeNull();
        found!.FriendlyName.Should().Be("Logitech BRIO");
    }

    [Fact]
    public void FindByDeviceId_CaseInsensitive()
    {
        var probe = new FakeProbe(
        [
            new MfDeviceRow(@"\\?\usb#VID_046D&PID_085E", "Cam", []),
        ]);

        var enumerator = new MediaFoundationEnumerator(probe);

        enumerator.FindByDeviceId(@"\\?\usb#vid_046d&pid_085e").Should().NotBeNull();
    }

    [Fact]
    public void FindByDeviceId_Missing_ReturnsNull()
    {
        var enumerator = new MediaFoundationEnumerator(new FakeProbe([]));

        enumerator.FindByDeviceId("nope").Should().BeNull();
    }

    [Fact]
    public void EnumerateDevices_MapsCapabilities_FromMfRow()
    {
        var probe = new FakeProbe(
        [
            new MfDeviceRow(
                "id",
                "Cam",
                Capabilities: new[]
                {
                    new MfCapability(1920, 1080, 30, "nv12"),
                    new MfCapability(1280, 720, 60, "mjpeg"),
                }),
        ]);

        var enumerator = new MediaFoundationEnumerator(probe);

        var devices = enumerator.EnumerateDevices(TestContext.Current.CancellationToken);

        devices.Should().HaveCount(1);
        devices[0].Capabilities.Should().HaveCount(2);
        devices[0].Capabilities[0].Width.Should().Be(1920);
        devices[0].Capabilities[0].Height.Should().Be(1080);
        devices[0].Capabilities[0].FrameRate.Should().Be(30);
        devices[0].Capabilities[0].PixelFormat.Should().Be("nv12");
        devices[0].Capabilities[1].PixelFormat.Should().Be("mjpeg");
    }

    [Fact]
    public void FindByFriendlyName_ReturnsMatchingDescriptor()
    {
        var probe = new FakeProbe(
        [
            new MfDeviceRow("id", "Logitech BRIO", []),
        ]);

        var enumerator = new MediaFoundationEnumerator(probe);

        enumerator.FindByFriendlyName("Logitech BRIO").Should().NotBeNull();
        enumerator.FindByFriendlyName("logitech brio").Should().NotBeNull();
        enumerator.FindByFriendlyName("Other").Should().BeNull();
    }

    private sealed class FakeProbe : IMfDeviceProbe
    {
        private readonly IReadOnlyList<MfDeviceRow> rows;

        public FakeProbe(IReadOnlyList<MfDeviceRow> rows)
        {
            this.rows = rows;
        }

        public IReadOnlyList<MfDeviceRow> EnumerateVideoCaptureDevices()
            => rows;
    }
}