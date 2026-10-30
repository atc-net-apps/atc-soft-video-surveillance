namespace AtcSoft.VideoEngine.Windows.MediaFoundation;

public class PixelFormatGuidMapperTests
{
    [Theory]
    [InlineData("3231564E-0000-0010-8000-00AA00389B71", "nv12")]
    [InlineData("32595559-0000-0010-8000-00AA00389B71", "yuyv422")]
    [InlineData("59565955-0000-0010-8000-00AA00389B71", "uyvy422")]
    [InlineData("47504A4D-0000-0010-8000-00AA00389B71", "mjpeg")]
    [InlineData("34363248-0000-0010-8000-00AA00389B71", "h264")]
    [InlineData("35363248-0000-0010-8000-00AA00389B71", "hevc")]
    [InlineData("43564548-0000-0010-8000-00AA00389B71", "hevc")]
    [InlineData("30323449-0000-0010-8000-00AA00389B71", "yuv420p")]
    [InlineData("56555949-0000-0010-8000-00AA00389B71", "yuv420p")]
    [InlineData("32315659-0000-0010-8000-00AA00389B71", "yuv420p")]
    public void Map_KnownGuid_ReturnsExpectedFFmpegString(
        string subtype,
        string expected)
    {
        PixelFormatGuidMapper.Map(new Guid(subtype)).Should().Be(expected);
    }

    [Fact]
    public void Map_UnknownGuid_ReturnsNull()
    {
        PixelFormatGuidMapper.Map(Guid.NewGuid()).Should().BeNull();
    }

    [Fact]
    public void Map_EmptyGuid_ReturnsNull()
    {
        PixelFormatGuidMapper.Map(Guid.Empty).Should().BeNull();
    }
}