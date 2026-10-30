namespace AtcSoft.VideoSurveillance.Extensions;

public class CameraProtocolExtensionsTests
{
    [Theory]
    [InlineData(CameraProtocol.Rtsp, "rtsp")]
    [InlineData(CameraProtocol.Http, "http")]
    [InlineData(CameraProtocol.Https, "https")]
    public void ToScheme_Returns_Correct_Scheme(
        CameraProtocol protocol,
        string expected)
    {
        // Act
        var result = protocol.ToScheme();

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void ToScheme_UnknownValue_Returns_Rtsp_Default()
    {
        // Arrange
        var unknown = (CameraProtocol)999;

        // Act
        var result = unknown.ToScheme();

        // Assert
        result.Should().Be("rtsp");
    }
}