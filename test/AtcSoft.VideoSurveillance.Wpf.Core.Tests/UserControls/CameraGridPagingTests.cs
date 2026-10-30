namespace AtcSoft.VideoSurveillance.Wpf.Core.UserControls;

using UiCameraConfiguration = AtcSoft.VideoSurveillance.Wpf.Core.Models.CameraConfiguration;

/// <summary>
/// The real camera grid with real tiles (without cameras behind them): a
/// fixed grid size shows one page of tiles, and the tiles of the other
/// pages are still there, only not shown.
/// </summary>
public sealed class CameraGridPagingTests
{
    [Fact]
    public void A_Fixed_Grid_Shows_One_Page_And_Keeps_The_Other_Tiles_Alive()
    {
        var error = WpfTestThread.Run(() =>
        {
            var grid = CreateGridWith(cameras: 9);

            grid.GridPreset = GridPreset.TwoByTwo;
            Layout(grid);

            grid.GridRowCount.Should().Be(2);
            grid.GridColumnCount.Should().Be(2);
            grid.PageCount.Should().Be(3);
            grid.PageLabel.Should().Be("1 / 3");
            VisibleTiles(grid).Should().Equal(0, 1, 2, 3);
            Containers(grid).Should().HaveCount(9, "the tiles of the other pages keep their cameras open");
        });

        error.Should().BeNull();
    }

    [Fact]
    public void Paging_Goes_Round_In_Both_Directions()
    {
        var error = WpfTestThread.Run(() =>
        {
            var grid = CreateGridWith(cameras: 9);
            grid.GridPreset = GridPreset.TwoByTwo;
            var changes = 0;
            grid.PagingChanged += (_, _) => changes++;

            grid.ShowNextPage();
            Layout(grid);
            VisibleTiles(grid).Should().Equal(4, 5, 6, 7);

            grid.ShowNextPage();
            Layout(grid);
            grid.PageLabel.Should().Be("3 / 3");
            VisibleTiles(grid).Should().Equal(8);

            grid.ShowNextPage();
            Layout(grid);
            VisibleTiles(grid).Should().Equal(0, 1, 2, 3);

            grid.ShowPreviousPage();
            Layout(grid);
            VisibleTiles(grid).Should().Equal(8);
            changes.Should().BeGreaterThanOrEqualTo(4);
        });

        error.Should().BeNull();
    }

    [Fact]
    public void Back_To_Automatic_Every_Tile_Is_Shown_Again()
    {
        var error = WpfTestThread.Run(() =>
        {
            var grid = CreateGridWith(cameras: 9);
            grid.GridPreset = GridPreset.OneByOne;
            grid.ShowNextPage();
            Layout(grid);
            VisibleTiles(grid).Should().Equal(1);

            grid.GridPreset = GridPreset.Auto;
            Layout(grid);

            VisibleTiles(grid).Should().HaveCount(9);
            grid.PageCount.Should().Be(1);
            grid.PageLabel.Should().BeEmpty();
            grid.GridColumnCount.Should().Be(0);
        });

        error.Should().BeNull();
    }

    [Fact]
    public void Removing_Cameras_While_On_The_Last_Page_Falls_Back_To_A_Page_That_Exists()
    {
        var error = WpfTestThread.Run(() =>
        {
            var grid = CreateGridWith(cameras: 9);
            grid.GridPreset = GridPreset.TwoByTwo;
            grid.CurrentPage = 2;
            Layout(grid);

            grid.CameraTiles.RemoveAt(8);
            Layout(grid);

            grid.PageCount.Should().Be(2);
            grid.CurrentPage.Should().Be(1);
            VisibleTiles(grid).Should().Equal(4, 5, 6, 7);
        });

        error.Should().BeNull();
    }

    [Fact]
    public void A_Camera_Added_To_A_Full_Page_Appears_On_The_Next_Page_Not_On_This_One()
    {
        var error = WpfTestThread.Run(() =>
        {
            var grid = CreateGridWith(cameras: 4);
            grid.GridPreset = GridPreset.TwoByTwo;
            Layout(grid);
            grid.PageCount.Should().Be(1);

            grid.CameraTiles.Add(CreateCamera(4));
            Layout(grid);

            grid.PageCount.Should().Be(2);
            VisibleTiles(grid).Should().Equal(0, 1, 2, 3);
        });

        error.Should().BeNull();
    }

    private static CameraGrid CreateGridWith(int cameras)
    {
        var grid = new CameraGrid { Width = 800, Height = 600 };
        for (var i = 0; i < cameras; i++)
        {
            grid.CameraTiles.Add(CreateCamera(i));
        }

        Layout(grid);
        return grid;
    }

    private static UiCameraConfiguration CreateCamera(int index)
    {
        var camera = new UiCameraConfiguration { Id = Guid.NewGuid() };
        camera.Display.DisplayName = "Camera " + index;
        camera.Connection.IpAddress = "192.0.2." + (index + 1);
        return camera;
    }

    // Runs the layout pass that creates the tiles, and the work the grid
    // has queued behind it.
    private static void Layout(CameraGrid grid)
    {
        grid.Measure(new System.Windows.Size(800, 600));
        grid.Arrange(new System.Windows.Rect(0, 0, 800, 600));
        grid.UpdateLayout();
        System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(
            () => { },
            System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    }

    private static List<System.Windows.UIElement> Containers(CameraGrid grid)
    {
        var items = (System.Windows.Controls.ItemsControl)grid.FindName("CameraItemsControl");
        var containers = new List<System.Windows.UIElement>();
        for (var i = 0; i < items.Items.Count; i++)
        {
            if (items.ItemContainerGenerator.ContainerFromIndex(i) is System.Windows.UIElement container)
            {
                containers.Add(container);
            }
        }

        return containers;
    }

    private static List<int> VisibleTiles(CameraGrid grid)
        => [.. Containers(grid)
            .Select((container, index) => (container, index))
            .Where(x => x.container.Visibility == System.Windows.Visibility.Visible)
            .Select(x => x.index)];
}