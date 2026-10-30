namespace AtcSoft.VideoSurveillance.Enums;

public class CameraProtocolTests
{
    [Theory]
    [InlineData(CameraProtocol.Rtsp)]
    [InlineData(CameraProtocol.Http)]
    [InlineData(CameraProtocol.Https)]
    public void Enum_HasExpectedValues(CameraProtocol protocol)
    {
        // Assert
        Enum.IsDefined(typeof(CameraProtocol), protocol)
            .Should()
            .BeTrue();
    }
}