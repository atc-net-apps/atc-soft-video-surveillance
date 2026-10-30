namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

public sealed class CameraFormLogicTests
{
    private static readonly UsbStreamFormat[] Capabilities =
    [
        new(Width: 640, Height: 480, FrameRate: 30, PixelFormat: "yuyv422"),
        new(Width: 1920, Height: 1080, FrameRate: 30, PixelFormat: "yuyv422"),
        new(Width: 1280, Height: 720, FrameRate: 15, PixelFormat: "nv12"),
        new(Width: 1280, Height: 720, FrameRate: 30, PixelFormat: "nv12"),
        new(Width: 1280, Height: 720, FrameRate: 30, PixelFormat: "yuyv422"),
        new(Width: 2560, Height: 1440, FrameRate: 30, PixelFormat: "yuyv422"),
    ];

    [Fact]
    public void The_Sizes_A_Camera_Offers_Are_Listed_Once_Each_Largest_First()
        => UsbFormatChoices.Sizes(Capabilities)
            .Should().Equal((2560, 1440), (1920, 1080), (1280, 720), (640, 480));

    [Fact]
    public void The_Frame_Rates_Are_Those_Of_The_Chosen_Size_Highest_First()
    {
        UsbFormatChoices.FrameRates(Capabilities, 1280, 720).Should().Equal(30, 15);
        UsbFormatChoices.FrameRates(Capabilities, 640, 480).Should().Equal(30);
        UsbFormatChoices.FrameRates(Capabilities, 800, 600).Should().BeEmpty();
    }

    [Fact]
    public void A_New_Camera_Starts_At_720p_When_The_Camera_Has_It()
    {
        // Enough to see who is there, and light enough to encode and watch.
        UsbFormatChoices.Default(Capabilities).Should().Be((1280, 720, 30));
    }

    [Fact]
    public void Without_720p_A_New_Camera_Starts_At_The_Largest_Size_Up_To_Full_Hd()
    {
        UsbStreamFormat[] capabilities =
        [
            new(Width: 640, Height: 480, FrameRate: 30, PixelFormat: "yuyv422"),
            new(Width: 1920, Height: 1080, FrameRate: 15, PixelFormat: "yuyv422"),
            new(Width: 3840, Height: 2160, FrameRate: 30, PixelFormat: "yuyv422"),
        ];

        UsbFormatChoices.Default(capabilities).Should().Be((1920, 1080, 15));
    }

    [Fact]
    public void A_Camera_That_Tells_Nothing_About_Itself_Is_Left_To_Its_Own_Default()
        => UsbFormatChoices.Default([]).Should().Be((0, 0, 0));

    [Fact]
    public void A_Usb_Camera_Is_Shown_By_Its_Device_Name()
    {
        UsbFormatChoices.DescribeSource(isUsb: true, "HP 5MP Camera", protocol: null, "192.168.1.10", 554, "stream1")
            .Should().Be("USB: HP 5MP Camera");
        UsbFormatChoices.DescribeSource(isUsb: true, usbName: null, protocol: null, string.Empty, 0, path: null)
            .Should().Be("USB");
    }

    [Theory]
    [InlineData("Rtsp", "192.168.1.10", 554, "stream1", "rtsp://192.168.1.10:554/stream1")]
    [InlineData("Http", "cam.local", 8080, "/live", "http://cam.local:8080/live")]
    [InlineData(null, "192.168.1.10", 554, null, "rtsp://192.168.1.10:554")]
    public void A_Network_Camera_Is_Shown_By_Its_Address_Without_Its_Credentials(
        string? protocol,
        string host,
        int port,
        string? path,
        string expected)
        => UsbFormatChoices.DescribeSource(isUsb: false, usbName: null, protocol, host, port, path)
            .Should().Be(expected);
}