namespace AtcSoft.VideoSurveillance.Models;

public class UsbStreamFormatTests
{
    [Fact]
    public void New_Instance_Has_Default_Values()
    {
        var format = new UsbStreamFormat();

        format.Width.Should().Be(0);
        format.Height.Should().Be(0);
        format.FrameRate.Should().Be(0);
        format.PixelFormat.Should().BeEmpty();
    }

    [Fact]
    public void Clone_Creates_Deep_Copy()
    {
        var original = new UsbStreamFormat
        {
            Width = 1920,
            Height = 1080,
            FrameRate = 29.97,
            PixelFormat = "nv12",
        };

        var clone = original.Clone();
        clone.Width = 640;

        original.Width.Should().Be(1920);
        clone.Width.Should().Be(640);
    }

    [Fact]
    public void CopyFrom_Replaces_All_Fields()
    {
        var target = new UsbStreamFormat();
        var source = new UsbStreamFormat
        {
            Width = 1280,
            Height = 720,
            FrameRate = 60,
            PixelFormat = "mjpeg",
        };

        target.CopyFrom(source);

        target.Width.Should().Be(1280);
        target.Height.Should().Be(720);
        target.FrameRate.Should().Be(60);
        target.PixelFormat.Should().Be("mjpeg");
    }

    [Fact]
    public void ValueEquals_Returns_True_For_Same_Values()
    {
        var a = new UsbStreamFormat { Width = 1920, Height = 1080, FrameRate = 30, PixelFormat = "nv12" };
        var b = new UsbStreamFormat { Width = 1920, Height = 1080, FrameRate = 30, PixelFormat = "nv12" };

        a.ValueEquals(b).Should().BeTrue();
    }

    [Fact]
    public void ValueEquals_Returns_False_When_PixelFormat_Differs()
    {
        var a = new UsbStreamFormat { Width = 1920, Height = 1080, FrameRate = 30, PixelFormat = "nv12" };
        var b = new UsbStreamFormat { Width = 1920, Height = 1080, FrameRate = 30, PixelFormat = "yuyv422" };

        a.ValueEquals(b).Should().BeFalse();
    }

    [Fact]
    public void ValueEquals_Returns_False_For_Null()
        => new UsbStreamFormat().ValueEquals(null).Should().BeFalse();

    [Fact]
    public void ToString_Includes_Resolution_FrameRate_PixelFormat()
    {
        var format = new UsbStreamFormat
        {
            Width = 1920,
            Height = 1080,
            FrameRate = 29.97,
            PixelFormat = "nv12",
        };

        format.ToString().Should().Be("1920x1080@29.97 nv12");
    }

    [Fact]
    public void ToString_Trims_Trailing_Space_When_PixelFormat_Empty()
    {
        var format = new UsbStreamFormat { Width = 640, Height = 480, FrameRate = 30 };

        format.ToString().Should().Be("640x480@30");
    }
}