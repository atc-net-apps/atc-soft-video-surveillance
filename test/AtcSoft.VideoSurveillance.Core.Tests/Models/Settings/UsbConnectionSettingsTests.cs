namespace AtcSoft.VideoSurveillance.Models.Settings;

public class UsbConnectionSettingsTests
{
    [Fact]
    public void New_Instance_Has_Default_Values()
    {
        var usb = new UsbConnectionSettings();

        usb.DeviceId.Should().BeEmpty();
        usb.FriendlyName.Should().BeEmpty();
        usb.Format.Should().BeNull();
        usb.PreferAudio.Should().BeFalse();
        usb.AudioDeviceName.Should().BeEmpty();
    }

    [Fact]
    public void Clone_Creates_Deep_Copy_Including_Format()
    {
        var original = new UsbConnectionSettings
        {
            DeviceId = @"\\?\usb#vid_046d&pid_085e",
            FriendlyName = "Logitech BRIO",
            Format = new UsbStreamFormat { Width = 1920, Height = 1080, FrameRate = 30, PixelFormat = "nv12" },
            PreferAudio = true,
        };

        var clone = original.Clone();
        clone.Format!.Width = 640;

        original.Format!.Width.Should().Be(1920);
        clone.Format.Width.Should().Be(640);
        clone.DeviceId.Should().Be(original.DeviceId);
        clone.PreferAudio.Should().BeTrue();
    }

    [Fact]
    public void Clone_Without_Format_Returns_Null_Format()
    {
        var original = new UsbConnectionSettings { DeviceId = "abc" };

        original.Clone().Format.Should().BeNull();
    }

    [Fact]
    public void ValueEquals_Returns_True_For_Identical_Settings()
    {
        var a = new UsbConnectionSettings
        {
            DeviceId = "x",
            FriendlyName = "Cam",
            Format = new UsbStreamFormat { Width = 1280, Height = 720, FrameRate = 60, PixelFormat = "mjpeg" },
        };
        var b = new UsbConnectionSettings
        {
            DeviceId = "x",
            FriendlyName = "Cam",
            Format = new UsbStreamFormat { Width = 1280, Height = 720, FrameRate = 60, PixelFormat = "mjpeg" },
        };

        a.ValueEquals(b).Should().BeTrue();
    }

    [Fact]
    public void ValueEquals_Returns_False_When_DeviceId_Differs()
    {
        var a = new UsbConnectionSettings { DeviceId = "x" };
        var b = new UsbConnectionSettings { DeviceId = "y" };

        a.ValueEquals(b).Should().BeFalse();
    }

    [Fact]
    public void ValueEquals_Treats_Both_Null_Formats_As_Equal()
    {
        var a = new UsbConnectionSettings { DeviceId = "x" };
        var b = new UsbConnectionSettings { DeviceId = "x" };

        a.ValueEquals(b).Should().BeTrue();
    }

    [Fact]
    public void ValueEquals_Returns_False_When_Only_One_Format_Is_Null()
    {
        var a = new UsbConnectionSettings { DeviceId = "x" };
        var b = new UsbConnectionSettings
        {
            DeviceId = "x",
            Format = new UsbStreamFormat { Width = 640, Height = 480, FrameRate = 30 },
        };

        a.ValueEquals(b).Should().BeFalse();
        b.ValueEquals(a).Should().BeFalse();
    }

    [Fact]
    public void Clone_PropagatesAudioDeviceName()
    {
        var original = new UsbConnectionSettings
        {
            DeviceId = "x",
            PreferAudio = true,
            AudioDeviceName = "Microphone (Logitech BRIO)",
        };

        var clone = original.Clone();

        clone.AudioDeviceName.Should().Be("Microphone (Logitech BRIO)");
        clone.PreferAudio.Should().BeTrue();
    }

    [Fact]
    public void ValueEquals_DistinguishesAudioDeviceName()
    {
        // Two cameras pointing at the same video device but different
        // mic endpoints are not the same camera — pin that here so a
        // future refactor can't silently drop AudioDeviceName from the
        // equality check (the same trap that hit Phase 5's converter).
        var a = new UsbConnectionSettings { DeviceId = "x", AudioDeviceName = "Mic A" };
        var b = new UsbConnectionSettings { DeviceId = "x", AudioDeviceName = "Mic B" };

        a.ValueEquals(b).Should().BeFalse();
    }
}