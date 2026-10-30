namespace AtcSoft.VideoSurveillance.Wpf.Core.Helpers;

/// <summary>
/// A grid of a fixed size shows one page of cameras at a time. The cameras
/// that are not on the page stay connected; they are only not shown.
/// </summary>
public sealed class GridPagerTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(9)]
    [InlineData(40)]
    public void Automatic_Shows_Every_Camera_On_One_Page(int cameras)
    {
        var page = GridPager.Calculate(cameras, GridPreset.Auto, pageIndex: 2);

        page.PageCount.Should().Be(1);
        page.PageIndex.Should().Be(0);
        page.FirstVisible.Should().Be(0);
        page.VisibleCount.Should().Be(cameras);
        page.Rows.Should().Be(GridLayoutHelper.CalculateRowCount(cameras));
        page.Columns.Should().Be(0, "the columns follow from the rows and the number of cameras");
    }

    [Theory]
    [InlineData(GridPreset.OneByOne, 1)]
    [InlineData(GridPreset.TwoByTwo, 2)]
    [InlineData(GridPreset.ThreeByThree, 3)]
    [InlineData(GridPreset.FourByFour, 4)]
    public void A_Fixed_Grid_Keeps_Its_Size_Whatever_The_Number_Of_Cameras(
        GridPreset preset,
        int side)
    {
        // Three cameras in a 4 x 4 grid stay small: the grid is what was asked for.
        var page = GridPager.Calculate(cameraCount: 3, preset, pageIndex: 0);

        page.Rows.Should().Be(side);
        page.Columns.Should().Be(side);
    }

    [Theory]
    [InlineData(9, GridPreset.TwoByTwo, 3)]
    [InlineData(8, GridPreset.TwoByTwo, 2)]
    [InlineData(9, GridPreset.ThreeByThree, 1)]
    [InlineData(10, GridPreset.ThreeByThree, 2)]
    [InlineData(9, GridPreset.OneByOne, 9)]
    [InlineData(0, GridPreset.TwoByTwo, 1)]
    public void The_Cameras_Are_Spread_Over_As_Many_Pages_As_It_Takes(
        int cameras,
        GridPreset preset,
        int expectedPages)
        => GridPager.Calculate(cameras, preset, pageIndex: 0).PageCount.Should().Be(expectedPages);

    [Fact]
    public void Each_Page_Shows_The_Next_Cameras_And_The_Last_Shows_What_Is_Left()
    {
        var first = GridPager.Calculate(9, GridPreset.TwoByTwo, pageIndex: 0);
        var second = GridPager.Calculate(9, GridPreset.TwoByTwo, pageIndex: 1);
        var last = GridPager.Calculate(9, GridPreset.TwoByTwo, pageIndex: 2);

        (first.FirstVisible, first.VisibleCount).Should().Be((0, 4));
        (second.FirstVisible, second.VisibleCount).Should().Be((4, 4));
        (last.FirstVisible, last.VisibleCount).Should().Be((8, 1));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(3, 2)]
    [InlineData(99, 2)]
    public void A_Page_That_Does_Not_Exist_Becomes_The_Nearest_That_Does(
        int asked,
        int expected)
    {
        // A camera was removed, or the grid was made larger, while a later page was shown.
        GridPager.Calculate(9, GridPreset.TwoByTwo, asked).PageIndex.Should().Be(expected);
    }

    [Theory]
    [InlineData(0, true, false)]
    [InlineData(3, true, false)]
    [InlineData(4, false, true)]
    [InlineData(7, false, true)]
    [InlineData(8, false, false)]
    public void A_Camera_Is_Shown_When_It_Is_On_The_Page(
        int cameraIndex,
        bool onFirstPage,
        bool onSecondPage)
    {
        GridPager.Calculate(9, GridPreset.TwoByTwo, 0).Shows(cameraIndex).Should().Be(onFirstPage);
        GridPager.Calculate(9, GridPreset.TwoByTwo, 1).Shows(cameraIndex).Should().Be(onSecondPage);
    }

    [Theory]
    [InlineData("0", GridPreset.Auto)]
    [InlineData("1", GridPreset.OneByOne)]
    [InlineData("2", GridPreset.TwoByTwo)]
    [InlineData("3", GridPreset.ThreeByThree)]
    [InlineData("4", GridPreset.FourByFour)]
    [InlineData("7", GridPreset.Auto)]
    [InlineData(null, GridPreset.Auto)]
    public void A_Preset_Is_Picked_By_Its_Number_Of_Cameras_Along_A_Side(
        string? number,
        GridPreset expected)
        => GridPager.ParsePreset(number).Should().Be(expected);

    [Theory]
    [InlineData(0, 1, "")]
    [InlineData(0, 3, "1 / 3")]
    [InlineData(2, 3, "3 / 3")]
    public void The_Page_Is_Only_Named_When_There_Is_More_Than_One(
        int pageIndex,
        int pageCount,
        string expected)
        => new GridPage(2, 2, pageCount, pageIndex, 0, 4).Label.Should().Be(expected);
}