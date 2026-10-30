namespace AtcSoft.VideoSurveillance.Helpers;

public class SourceLocatorTests
{
    [Fact]
    public void Constructor_NetworkOnly_PopulatesUriAndLeavesRestNull()
    {
        var locator = new SourceLocator(new Uri("rtsp://10.0.0.1:554/stream1"));

        locator.Uri.AbsoluteUri.Should().Be("rtsp://10.0.0.1:554/stream1");
        locator.InputFormat.Should().BeNull();
        locator.RawDeviceSpec.Should().BeNull();
        locator.VideoSize.Should().BeNull();
        locator.FrameRate.Should().BeNull();
        locator.PixelFormat.Should().BeNull();
        locator.IsLocalDevice.Should().BeFalse();
    }

    [Fact]
    public void Constructor_DshowSource_ReportsLocalDevice()
    {
        var locator = new SourceLocator(
            uri: new Uri("dshow:Logitech%20BRIO"),
            inputFormat: "dshow",
            rawDeviceSpec: "video=Logitech BRIO",
            videoSize: "1920x1080",
            frameRate: "30",
            pixelFormat: "nv12");

        locator.IsLocalDevice.Should().BeTrue();
        locator.InputFormat.Should().Be("dshow");
        locator.RawDeviceSpec.Should().Be("video=Logitech BRIO");
        locator.VideoSize.Should().Be("1920x1080");
        locator.FrameRate.Should().Be("30");
        locator.PixelFormat.Should().Be("nv12");
    }

    [Fact]
    public void Constructor_TreatsEmptyOptionalsAsNull()
    {
        var locator = new SourceLocator(
            uri: new Uri("dshow:cam"),
            inputFormat: string.Empty,
            rawDeviceSpec: string.Empty,
            videoSize: string.Empty,
            frameRate: string.Empty,
            pixelFormat: string.Empty);

        locator.InputFormat.Should().BeNull();
        locator.RawDeviceSpec.Should().BeNull();
        locator.VideoSize.Should().BeNull();
        locator.FrameRate.Should().BeNull();
        locator.PixelFormat.Should().BeNull();
        locator.IsLocalDevice.Should().BeFalse();
    }

    [Fact]
    public void Constructor_NullUri_Throws()
    {
        var act = () => new SourceLocator(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}