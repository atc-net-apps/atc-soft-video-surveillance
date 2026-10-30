namespace AtcSoft.VideoSurveillance.Wpf.Core.Factories;

public class DropDownItemsFactoryConnectionTypeTests
{
    [Fact]
    public void ConnectionTypeItems_Offers_Network_Types_Only()
    {
        // USB is implied by the camera source, never picked by hand.
        DropDownItemsFactory.ConnectionTypeItems.Keys.Should().BeEquivalentTo(
            nameof(CameraConnectionType.None),
            nameof(CameraConnectionType.Lan),
            nameof(CameraConnectionType.Wifi));
    }

    [Fact]
    public void ConnectionTypeItems_Keys_Parse_To_Enum()
    {
        foreach (var key in DropDownItemsFactory.ConnectionTypeItems.Keys)
        {
            Enum.TryParse<CameraConnectionType>(key, out _).Should().BeTrue();
        }
    }

    [Theory]
    [InlineData(OverlayPosition.TopLeft, OverlayPosition.TopRight, OverlayPosition.BottomLeft)]
    [InlineData(OverlayPosition.BottomRight, OverlayPosition.BottomLeft, OverlayPosition.TopRight)]
    public void GetConnectionTypeIconPlacementItems_Names_Concrete_Corners(
        OverlayPosition infoBox,
        OverlayPosition expectedSameRowCorner,
        OverlayPosition expectedSameColumnCorner)
    {
        var items = DropDownItemsFactory.GetConnectionTypeIconPlacementItems(infoBox);
        var cornerNames = DropDownItemsFactory.OverlayPositionItems;

        items.Keys.Should().BeEquivalentTo(
            nameof(ConnectionTypeIconPlacement.SameRow),
            nameof(ConnectionTypeIconPlacement.SameColumn));
        items[nameof(ConnectionTypeIconPlacement.SameRow)].Should().Contain(cornerNames[expectedSameRowCorner.ToString()]);
        items[nameof(ConnectionTypeIconPlacement.SameColumn)].Should().Contain(cornerNames[expectedSameColumnCorner.ToString()]);
    }
}