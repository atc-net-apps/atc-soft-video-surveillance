namespace AtcSoft.VideoSurveillance.Wpf.Core.Helpers;

using ConnectionState = Atc.Network.ConnectionState;

public class ConnectionTypeIconHelperTests
{
    [Theory]
    [InlineData(ConnectionState.Connected, ConnectionTypeIconTint.Normal)]
    [InlineData(ConnectionState.Reconnected, ConnectionTypeIconTint.Normal)]
    [InlineData(ConnectionState.Pulse, ConnectionTypeIconTint.Normal)]
    [InlineData(ConnectionState.None, ConnectionTypeIconTint.Normal)]
    [InlineData(ConnectionState.Connecting, ConnectionTypeIconTint.Warning)]
    [InlineData(ConnectionState.Reconnecting, ConnectionTypeIconTint.Warning)]
    [InlineData(ConnectionState.Disconnecting, ConnectionTypeIconTint.Warning)]
    [InlineData(ConnectionState.Disconnected, ConnectionTypeIconTint.Error)]
    [InlineData(ConnectionState.ConnectionFailed, ConnectionTypeIconTint.Error)]
    [InlineData(ConnectionState.ReconnectionFailed, ConnectionTypeIconTint.Error)]
    public void GetTint_Maps_Connection_State(
        ConnectionState state,
        ConnectionTypeIconTint expected)
    {
        ConnectionTypeIconHelper.GetTint(state).Should().Be(expected);
    }

    [Fact]
    public void GetTint_Covers_Every_Connection_State()
    {
        foreach (var state in Enum.GetValues<ConnectionState>())
        {
            var act = () => ConnectionTypeIconHelper.GetTint(state);

            act.Should().NotThrow();
        }
    }

    [Theory]
    [InlineData(CameraConnectionType.Lan)]
    [InlineData(CameraConnectionType.Wifi)]
    [InlineData(CameraConnectionType.Usb)]
    public void GetGeometryData_Returns_Parseable_Path_For_Known_Types(
        CameraConnectionType connectionType)
    {
        var data = ConnectionTypeIconHelper.GetGeometryData(connectionType);

        data.Should().NotBeNullOrWhiteSpace();
        var act = () => System.Windows.Media.Geometry.Parse(data);
        act.Should().NotThrow();
    }

    [Fact]
    public void GetGeometryData_Returns_Null_For_None()
    {
        ConnectionTypeIconHelper.GetGeometryData(CameraConnectionType.None).Should().BeNull();
    }

    [Fact]
    public void Glyphs_Are_Distinct()
    {
        var glyphs = new[]
        {
            ConnectionTypeIconHelper.GetGeometryData(CameraConnectionType.Lan),
            ConnectionTypeIconHelper.GetGeometryData(CameraConnectionType.Wifi),
            ConnectionTypeIconHelper.GetGeometryData(CameraConnectionType.Usb),
        };

        glyphs.Should().OnlyHaveUniqueItems();
    }

    [Theory]
    [InlineData(CameraConnectionType.None)]
    [InlineData(CameraConnectionType.Lan)]
    [InlineData(CameraConnectionType.Wifi)]
    [InlineData(CameraConnectionType.Usb)]
    public void GetDisplayName_Is_Not_Empty(CameraConnectionType connectionType)
    {
        ConnectionTypeIconHelper.GetDisplayName(connectionType).Should().NotBeNullOrWhiteSpace();
    }
}