namespace AtcSoft.VideoSurveillance.Wpf.Helpers;

public sealed class DropDownKeyTests
{
    private static readonly Dictionary<string, string> Positions = new(StringComparer.Ordinal)
    {
        ["TopLeft"] = "Top Left",
        ["BottomRight"] = "Bottom Right",
    };

    private static readonly Dictionary<string, string> Transports = new(StringComparer.Ordinal)
    {
        ["tcp"] = "TCP",
        ["udp"] = "UDP",
    };

    [Theory]
    [InlineData("bottomRight", "BottomRight")]
    [InlineData("BottomRight", "BottomRight")]
    public void The_Server_Value_Selects_The_Entry_Whatever_Its_Casing(
        string value,
        string expected)
        => DropDownKey.For(value, Positions, fallback: "TopLeft").Should().Be(expected);

    [Fact]
    public void A_Lowercase_Key_Is_Found_From_A_Capitalised_Value()
        => DropDownKey.For("Udp", Transports, fallback: "tcp").Should().Be("udp");

    [Theory]
    [InlineData("middle")]
    [InlineData("")]
    [InlineData(null)]
    public void A_Value_The_List_Does_Not_Have_Selects_The_Fallback(
        string? value)
        => DropDownKey.For(value, Positions, fallback: "TopLeft").Should().Be("TopLeft");
}