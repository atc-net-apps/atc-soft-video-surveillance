namespace AtcSoft.VideoSurveillance.Wpf.ViewModels;

public sealed class CameraItemViewModelFilterTests
{
    private static readonly CameraItemViewModel Camera = new()
    {
        DisplayName = "Front Door",
        Description = "Entrance, north side",
        IpAddress = "192.168.1.20",
        ConnectionState = "connected",
    };

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Without_A_Filter_Every_Camera_Matches(string? filter)
        => Camera.Matches(filter).Should().BeTrue();

    [Theory]
    [InlineData("front")]
    [InlineData("DOOR")]
    [InlineData("north")]
    [InlineData("1.20")]
    [InlineData("connected")]
    [InlineData("  front  ")]
    public void A_Camera_Matches_On_Its_Name_Description_Address_Or_State(
        string filter)
        => Camera.Matches(filter).Should().BeTrue();

    [Theory]
    [InlineData("garden")]
    [InlineData("10.0.0")]
    public void A_Camera_Does_Not_Match_Text_It_Does_Not_Have(string filter)
        => Camera.Matches(filter).Should().BeFalse();

    [Fact]
    public void Every_Word_Has_To_Match()
    {
        Camera.Matches("front north").Should().BeTrue();
        Camera.Matches("front garden").Should().BeFalse();
    }
}