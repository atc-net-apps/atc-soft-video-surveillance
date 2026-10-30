namespace AtcSoft.VideoSurveillance.Models;

public class UsbDeviceDescriptorTests
{
    [Fact]
    public void Constructor_Stores_All_Fields()
    {
        var caps = new[]
        {
            new UsbStreamFormat { Width = 1920, Height = 1080, FrameRate = 30, PixelFormat = "nv12" },
        };

        var descriptor = new UsbDeviceDescriptor(
            deviceId: @"\\?\usb#vid_046d&pid_085e#mi_00",
            friendlyName: "Logitech BRIO",
            vendorId: "046d",
            productId: "085e",
            isPresent: true,
            capabilities: caps);

        descriptor.DeviceId.Should().Be(@"\\?\usb#vid_046d&pid_085e#mi_00");
        descriptor.FriendlyName.Should().Be("Logitech BRIO");
        descriptor.VendorId.Should().Be("046d");
        descriptor.ProductId.Should().Be("085e");
        descriptor.IsPresent.Should().BeTrue();
        descriptor.Capabilities.Should().HaveCount(1);
    }

    [Fact]
    public void Constructor_Defaults_Capabilities_To_Empty_When_Null()
    {
        var descriptor = new UsbDeviceDescriptor("id", "name");

        descriptor.Capabilities.Should().BeEmpty();
        descriptor.IsPresent.Should().BeTrue();
    }

    [Fact]
    public void Constructor_Throws_When_DeviceId_Is_Empty()
    {
        var act = () => new UsbDeviceDescriptor(string.Empty, "name");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_Throws_When_FriendlyName_Is_Null()
    {
        var act = () => new UsbDeviceDescriptor("id", null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void IdentityEquals_Compares_DeviceId_Case_Insensitively()
    {
        var a = new UsbDeviceDescriptor(@"\\?\USB#VID_046D", "A");
        var b = new UsbDeviceDescriptor(@"\\?\usb#vid_046d", "B");

        a.IdentityEquals(b).Should().BeTrue();
    }

    [Fact]
    public void IdentityEquals_Returns_False_For_Null()
    {
        new UsbDeviceDescriptor("id", "name").IdentityEquals(null).Should().BeFalse();
    }
}