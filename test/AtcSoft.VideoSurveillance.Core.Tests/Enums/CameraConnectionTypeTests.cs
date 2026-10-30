namespace AtcSoft.VideoSurveillance.Enums;

public class CameraConnectionTypeTests
{
    [Theory]
    [InlineData(CameraConnectionType.None)]
    [InlineData(CameraConnectionType.Lan)]
    [InlineData(CameraConnectionType.Wifi)]
    [InlineData(CameraConnectionType.Usb)]
    public void Enum_HasExpectedValues(CameraConnectionType connectionType)
    {
        Enum.IsDefined(connectionType)
            .Should()
            .BeTrue();
    }

    [Fact]
    public void Enum_None_Is_Default_Zero_Value()
    {
        // Cameras stored before the connection type existed deserialize to
        // default(CameraConnectionType), which must mean "not set".
        default(CameraConnectionType).Should().Be(CameraConnectionType.None);
        ((int)CameraConnectionType.None).Should().Be(0);
    }

    [Fact]
    public void ConnectionTypeIconPlacement_SameRow_Is_Default_Zero_Value()
    {
        default(ConnectionTypeIconPlacement).Should().Be(ConnectionTypeIconPlacement.SameRow);
    }
}