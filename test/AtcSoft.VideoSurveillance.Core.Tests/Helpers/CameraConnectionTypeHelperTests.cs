namespace AtcSoft.VideoSurveillance.Helpers;

public class CameraConnectionTypeHelperTests
{
    [Theory]
    [InlineData(CameraConnectionType.None)]
    [InlineData(CameraConnectionType.Lan)]
    [InlineData(CameraConnectionType.Wifi)]
    [InlineData(CameraConnectionType.Usb)]
    public void GetEffective_Usb_Source_Is_Always_Usb(
        CameraConnectionType stored)
    {
        var connection = new ConnectionSettings
        {
            Source = CameraSource.Usb,
            ConnectionType = stored,
        };

        CameraConnectionTypeHelper.GetEffective(connection).Should().Be(CameraConnectionType.Usb);
    }

    [Theory]
    [InlineData(CameraConnectionType.None, CameraConnectionType.None)]
    [InlineData(CameraConnectionType.Lan, CameraConnectionType.Lan)]
    [InlineData(CameraConnectionType.Wifi, CameraConnectionType.Wifi)]
    [InlineData(CameraConnectionType.Usb, CameraConnectionType.None)]
    public void GetEffective_Network_Source_Uses_Stored_Value_Except_Usb(
        CameraConnectionType stored,
        CameraConnectionType expected)
    {
        var connection = new ConnectionSettings
        {
            Source = CameraSource.Network,
            ConnectionType = stored,
        };

        CameraConnectionTypeHelper.GetEffective(connection).Should().Be(expected);
    }

    [Fact]
    public void GetEffective_Throws_On_Null()
    {
        var act = () => CameraConnectionTypeHelper.GetEffective(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(OverlayPosition.TopLeft, ConnectionTypeIconPlacement.SameRow, OverlayPosition.TopRight)]
    [InlineData(OverlayPosition.TopLeft, ConnectionTypeIconPlacement.SameColumn, OverlayPosition.BottomLeft)]
    [InlineData(OverlayPosition.TopRight, ConnectionTypeIconPlacement.SameRow, OverlayPosition.TopLeft)]
    [InlineData(OverlayPosition.TopRight, ConnectionTypeIconPlacement.SameColumn, OverlayPosition.BottomRight)]
    [InlineData(OverlayPosition.BottomLeft, ConnectionTypeIconPlacement.SameRow, OverlayPosition.BottomRight)]
    [InlineData(OverlayPosition.BottomLeft, ConnectionTypeIconPlacement.SameColumn, OverlayPosition.TopLeft)]
    [InlineData(OverlayPosition.BottomRight, ConnectionTypeIconPlacement.SameRow, OverlayPosition.BottomLeft)]
    [InlineData(OverlayPosition.BottomRight, ConnectionTypeIconPlacement.SameColumn, OverlayPosition.TopRight)]
    public void ResolveIconCorner_Returns_Adjacent_Corner(
        OverlayPosition infoBox,
        ConnectionTypeIconPlacement placement,
        OverlayPosition expected)
    {
        CameraConnectionTypeHelper.ResolveIconCorner(infoBox, placement).Should().Be(expected);
    }

    [Fact]
    public void ResolveIconCorner_Never_Hits_InfoBox_Or_QuickActions_Corner()
    {
        foreach (var infoBox in Enum.GetValues<OverlayPosition>())
        {
            var quickActions = CameraConnectionTypeHelper.GetDiagonalCorner(infoBox);

            foreach (var placement in Enum.GetValues<ConnectionTypeIconPlacement>())
            {
                var icon = CameraConnectionTypeHelper.ResolveIconCorner(infoBox, placement);

                icon.Should().NotBe(infoBox);
                icon.Should().NotBe(quickActions);
            }
        }
    }

    [Theory]
    [InlineData(OverlayPosition.TopLeft, OverlayPosition.BottomRight)]
    [InlineData(OverlayPosition.TopRight, OverlayPosition.BottomLeft)]
    [InlineData(OverlayPosition.BottomLeft, OverlayPosition.TopRight)]
    [InlineData(OverlayPosition.BottomRight, OverlayPosition.TopLeft)]
    public void GetDiagonalCorner_Returns_Opposite_Corner(
        OverlayPosition corner,
        OverlayPosition expected)
    {
        CameraConnectionTypeHelper.GetDiagonalCorner(corner).Should().Be(expected);
    }

    [Theory]
    [InlineData(CameraConnectionType.None, false, CameraConnectionType.Wifi, CameraConnectionType.Wifi)]
    [InlineData(CameraConnectionType.Lan, false, CameraConnectionType.Wifi, CameraConnectionType.Lan)]
    [InlineData(CameraConnectionType.Lan, true, CameraConnectionType.Wifi, CameraConnectionType.Wifi)]
    [InlineData(CameraConnectionType.Lan, true, CameraConnectionType.None, CameraConnectionType.Lan)]
    [InlineData(CameraConnectionType.None, false, CameraConnectionType.None, CameraConnectionType.None)]
    public void ApplyDetected_Respects_Implicit_And_Explicit_Rules(
        CameraConnectionType current,
        bool isExplicit,
        CameraConnectionType detected,
        CameraConnectionType expected)
    {
        CameraConnectionTypeHelper.ApplyDetected(current, detected, isExplicit).Should().Be(expected);
    }
}