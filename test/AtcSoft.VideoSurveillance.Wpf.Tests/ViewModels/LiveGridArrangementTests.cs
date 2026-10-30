namespace AtcSoft.VideoSurveillance.Wpf.ViewModels;

/// <summary>
/// Which camera tiles the live view shows: those that match the filter,
/// one page of them at a time when the grid has a fixed size.
/// </summary>
public sealed class LiveGridArrangementTests
{
    private static readonly (string Name, string Description)[] Cameras =
    [
        ("Front door", "Entrance"),
        ("Garden", "Back of the house"),
        ("Garage", "Cars"),
        ("Back door", "Garden side"),
        ("Hall", string.Empty),
    ];

    [Fact]
    public void Without_A_Filter_On_Automatic_Every_Camera_Is_Shown()
    {
        var arrangement = LiveGridArrangement.Arrange(Cameras, filter: null, GridPreset.Auto, pageIndex: 0);

        arrangement.Shown.Should().OnlyContain(shown => shown);
        arrangement.MatchCount.Should().Be(5);
        (arrangement.Rows, arrangement.Columns).Should().Be(GridLayoutHelper.CalculateGridDimensions(5));
        arrangement.PageLabel.Should().BeEmpty();
    }

    [Theory]
    [InlineData("door", new[] { true, false, false, true, false })]
    [InlineData("GARDEN", new[] { false, true, false, true, false })]
    [InlineData("back door", new[] { false, false, false, true, false })]
    [InlineData("  ", new[] { true, true, true, true, true })]
    [InlineData("nothing", new[] { false, false, false, false, false })]
    public void The_Filter_Shows_The_Cameras_Whose_Name_Or_Description_Has_Every_Word(
        string filter,
        bool[] expected)
        => LiveGridArrangement.Arrange(Cameras, filter, GridPreset.Auto, pageIndex: 0)
            .Shown.Should().Equal(expected);

    [Fact]
    public void The_Grid_Is_Sized_For_The_Cameras_That_Match_Not_For_All()
    {
        var arrangement = LiveGridArrangement.Arrange(Cameras, "door", GridPreset.Auto, pageIndex: 0);

        arrangement.MatchCount.Should().Be(2);
        (arrangement.Rows, arrangement.Columns).Should().Be(GridLayoutHelper.CalculateGridDimensions(2));
    }

    [Fact]
    public void A_Fixed_Grid_Pages_Through_The_Cameras()
    {
        var first = LiveGridArrangement.Arrange(Cameras, filter: null, GridPreset.TwoByTwo, pageIndex: 0);
        var second = LiveGridArrangement.Arrange(Cameras, filter: null, GridPreset.TwoByTwo, pageIndex: 1);

        first.Shown.Should().Equal(true, true, true, true, false);
        first.PageLabel.Should().Be("1 / 2");
        (first.Rows, first.Columns).Should().Be((2, 2));
        second.Shown.Should().Equal(false, false, false, false, true);
        second.PageLabel.Should().Be("2 / 2");
    }

    [Fact]
    public void Pages_Are_Counted_Over_The_Cameras_That_Match_The_Filter()
    {
        // Two cameras match: with one camera per page that is two pages,
        // and the second page shows the second match, not the second camera.
        var arrangement = LiveGridArrangement.Arrange(Cameras, "door", GridPreset.OneByOne, pageIndex: 1);

        arrangement.Shown.Should().Equal(false, false, false, true, false);
        arrangement.PageCount.Should().Be(2);
        arrangement.PageIndex.Should().Be(1);
    }

    [Fact]
    public void A_Page_That_The_Filter_Left_Behind_Becomes_The_Last_One_There_Is()
    {
        var arrangement = LiveGridArrangement.Arrange(Cameras, "garage", GridPreset.OneByOne, pageIndex: 4);

        arrangement.PageIndex.Should().Be(0);
        arrangement.Shown.Should().Equal(false, false, true, false, false);
    }
}