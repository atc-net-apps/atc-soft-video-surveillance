namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

public sealed class UsbFieldsTests
{
    [Fact]
    public void A_Network_Camera_Sends_No_Usb_Settings()
    {
        UsbFields.Size(isUsb: false, 1280).Should().BeNull();
        UsbFields.FrameRate(isUsb: false, 30).Should().BeNull();
        UsbFields.Text(isUsb: false, "Logitech BRIO").Should().BeNull();
        UsbFields.Flag(isUsb: false, value: true).Should().BeNull();
    }

    [Fact]
    public void A_Usb_Camera_Sends_Its_Settings()
    {
        UsbFields.Size(isUsb: true, 1280).Should().Be(1280);
        UsbFields.FrameRate(isUsb: true, 30).Should().Be(30);
        UsbFields.Text(isUsb: true, "Logitech BRIO").Should().Be("Logitech BRIO");
        UsbFields.Flag(isUsb: true, value: false).Should().BeFalse();
    }

    [Fact]
    public void A_Usb_Camera_Without_A_Chosen_Format_Leaves_The_Format_Out()
    {
        // No format chosen is 0, which the server refuses as a size.
        UsbFields.Size(isUsb: true, 0).Should().BeNull();
        UsbFields.FrameRate(isUsb: true, 0).Should().BeNull();
    }
}