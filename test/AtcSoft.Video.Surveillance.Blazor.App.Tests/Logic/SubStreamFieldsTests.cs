namespace AtcSoft.Video.Surveillance.Blazor.App.Logic;

public sealed class SubStreamFieldsTests
{
    [Fact]
    public void A_Usb_Camera_Sends_No_Sub_Stream()
    {
        SubStreamFields.Path(isUsb: true, "/stream2", isUpdate: true).Should().BeNull();
        SubStreamFields.Port(isUsb: true, "/stream2", 8554, isUpdate: true).Should().BeNull();
    }

    [Fact]
    public void A_New_Camera_Without_A_Sub_Stream_Leaves_It_Out()
    {
        SubStreamFields.Path(isUsb: false, "  ", isUpdate: false).Should().BeNull();
        SubStreamFields.Port(isUsb: false, "  ", 8554, isUpdate: false).Should().BeNull();
    }

    [Fact]
    public void A_New_Camera_Sends_Its_Sub_Stream_And_A_Port_Only_When_It_Has_One()
    {
        SubStreamFields.Path(isUsb: false, " /stream2 ", isUpdate: false).Should().Be("/stream2");
        SubStreamFields.Port(isUsb: false, "/stream2", 0, isUpdate: false).Should().BeNull();
        SubStreamFields.Port(isUsb: false, "/stream2", 8554, isUpdate: false).Should().Be(8554);
    }

    [Fact]
    public void An_Update_Without_A_Sub_Stream_Removes_It_And_Its_Port()
    {
        // "" removes the sub-stream on the server, 0 is the main stream's port.
        SubStreamFields.Path(isUsb: false, string.Empty, isUpdate: true).Should().BeEmpty();
        SubStreamFields.Port(isUsb: false, string.Empty, 8554, isUpdate: true).Should().Be(0);
    }

    [Fact]
    public void An_Update_Sends_The_Sub_Stream_With_Its_Port()
    {
        SubStreamFields.Path(isUsb: false, "/cam/realmonitor?channel=1&subtype=1", isUpdate: true).Should().Be("/cam/realmonitor?channel=1&subtype=1");
        SubStreamFields.Port(isUsb: false, "/stream2", 8554, isUpdate: true).Should().Be(8554);
        SubStreamFields.Port(isUsb: false, "/stream2", 0, isUpdate: true).Should().Be(0);
    }
}