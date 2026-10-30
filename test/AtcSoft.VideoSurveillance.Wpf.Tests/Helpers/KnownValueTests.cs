namespace AtcSoft.VideoSurveillance.Wpf.Helpers;

public sealed class KnownValueTests
{
    [Theory]
    [InlineData("BottomRight")]
    [InlineData("bottomRight")]
    public void A_Name_Selects_The_Listed_Value_Whatever_Its_Casing(string name)
        => KnownValue.FromName(AppSettingsOverlayPosition.KnownValues, name, AppSettingsOverlayPosition.TopLeft)
            .Should().Be(AppSettingsOverlayPosition.BottomRight);

    [Theory]
    [InlineData("Middle")]
    [InlineData("")]
    [InlineData(null)]
    public void A_Name_The_Specification_Does_Not_List_Is_The_Fallback(
        string? name)
    {
        var value = KnownValue.FromName(AppSettingsOverlayPosition.KnownValues, name, AppSettingsOverlayPosition.TopLeft);

        value.Should().Be(AppSettingsOverlayPosition.TopLeft);
        value.IsKnown.Should().BeTrue();
    }

    [Fact]
    public void Every_Choice_Of_The_Settings_Dialog_Is_A_Value_The_Api_Lists()
    {
        // The dialog sends its choice by name; a name the API does not list would be sent as the fallback.
        Unlisted(DropDownItemsFactory.OverlayPositionItems, AppSettingsOverlayPosition.KnownValues).Should().BeEmpty();
        Unlisted(DropDownItemsFactory.GetConnectionTypeIconPlacementItems(AtcSoft.VideoSurveillance.Enums.OverlayPosition.TopLeft), AppSettingsConnectionTypeIconPlacement.KnownValues).Should().BeEmpty();
        Unlisted(DropDownItemsFactory.ProtocolItems, AppSettingsDefaultProtocol.KnownValues).Should().BeEmpty();
        Unlisted(DropDownItemsFactory.RtspTransportItems, AppSettingsRtspTransport.KnownValues).Should().BeEmpty();
        Unlisted(DropDownItemsFactory.RecordingFormatItems, AppSettingsRecordingFormat.KnownValues).Should().BeEmpty();
        Unlisted(DropDownItemsFactory.MediaCleanupScheduleItems, AppSettingsCleanupSchedule.KnownValues).Should().BeEmpty();
    }

    private static List<string> Unlisted<T>(
        IDictionary<string, string> items,
        IReadOnlyList<T> knownValues)
        where T : struct
        => items.Keys
            .Where(key => EqualityComparer<T>.Default.Equals(KnownValue.FromName(knownValues, key, default(T)), default))
            .ToList();
}