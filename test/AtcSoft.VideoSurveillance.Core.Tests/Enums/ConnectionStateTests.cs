namespace AtcSoft.VideoSurveillance.Enums;

public class ConnectionStateTests
{
    [Fact]
    public void Enum_HasExpectedMembers()
    {
        // Act
        var values = Enum.GetValues<ConnectionState>();

        // Assert
        values.Should().Contain(ConnectionState.Disconnected);
        values.Should().Contain(ConnectionState.Connecting);
        values.Should().Contain(ConnectionState.Connected);
        values.Should().Contain(ConnectionState.Reconnecting);
        values.Should().Contain(ConnectionState.Error);
    }
}