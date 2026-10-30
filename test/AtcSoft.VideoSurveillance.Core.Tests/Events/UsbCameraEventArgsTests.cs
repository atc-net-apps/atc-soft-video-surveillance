namespace AtcSoft.VideoSurveillance.Events;

public class UsbCameraEventArgsTests
{
    [Fact]
    public void Constructor_Stores_Device()
    {
        var device = new UsbDeviceDescriptor("id", "Cam");

        var args = new UsbCameraEventArgs(device);

        args.Device.Should().BeSameAs(device);
    }

    [Fact]
    public void Constructor_Throws_When_Device_Is_Null()
    {
        var act = () => new UsbCameraEventArgs(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}