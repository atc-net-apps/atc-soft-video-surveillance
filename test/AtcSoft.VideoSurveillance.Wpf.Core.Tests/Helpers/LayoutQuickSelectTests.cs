namespace AtcSoft.VideoSurveillance.Wpf.Core.Helpers;

using CameraLayout = AtcSoft.VideoSurveillance.Wpf.Core.Models.CameraLayout;

public sealed class LayoutQuickSelectTests
{
    private static readonly CameraLayout[] Layouts =
    [
        new() { Name = "Outside" },
        new() { Name = "Inside" },
        new() { Name = "Garage" },
    ];

    [Theory]
    [InlineData(1, "Outside")]
    [InlineData(2, "Inside")]
    [InlineData(3, "Garage")]
    public void A_Number_Picks_The_Layout_In_That_Place_Of_The_List(
        int number,
        string expected)
        => LayoutQuickSelect.GetByNumber(Layouts, number)!.Name.Should().Be(expected);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(9)]
    public void A_Number_Without_A_Layout_Picks_Nothing(int number)
        => LayoutQuickSelect.GetByNumber(Layouts, number).Should().BeNull();

    [Theory]
    [InlineData("2", "Inside")]
    [InlineData(" 3 ", "Garage")]
    public void The_Number_Can_Come_As_Text_From_A_Key_Binding(
        string number,
        string expected)
        => LayoutQuickSelect.GetByNumber(Layouts, number)!.Name.Should().Be(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("two")]
    public void Text_That_Is_Not_A_Number_Picks_Nothing(string? number)
        => LayoutQuickSelect.GetByNumber(Layouts, number).Should().BeNull();
}