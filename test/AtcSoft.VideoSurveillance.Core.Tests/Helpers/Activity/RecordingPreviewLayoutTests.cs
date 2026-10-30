namespace AtcSoft.VideoSurveillance.Helpers.Activity;

/// <summary>
/// The layout of a recording's previews: a grid evenly over the recording, a tile for the busiest
/// second of each event, tiles the shape of the picture, and the look-ups a timeline makes.
/// </summary>
public sealed class RecordingPreviewLayoutTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "atcsoft-previewlayout-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            Directory.Delete(folder, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
            // Never made.
        }
    }

    [Theory]
    [InlineData(1920, 1080, 160, 90)]
    [InlineData(1080, 1920, 90, 160)]
    [InlineData(640, 480, 160, 120)]
    [InlineData(0, 0, 160, 90)]
    public void A_Tile_Has_The_Shape_Of_The_Picture_Its_Longest_Side_160(
        int displayWidth,
        int displayHeight,
        int width,
        int height)
        => RecordingPreviewLayout.TileSize(displayWidth, displayHeight).Should().Be((width, height));

    [Fact]
    public void A_Short_Recording_Has_A_Tile_A_Second_And_A_Long_One_At_Most_600()
    {
        var (shortOne, shortMoments) = RecordingPreviewLayout.Plan(3, 1920, 1080, []);
        var (hour, _) = RecordingPreviewLayout.Plan(3600, 1920, 1080, []);

        (shortOne.IntervalSeconds, shortOne.GridCount).Should().Be((1d, 4));
        shortMoments.Select(m => m.Seconds).Should().Equal(0, 1, 2, 3);
        hour.GridCount.Should().Be(RecordingPreviewLayout.MostGridTiles);
        ((hour.GridCount - 1) * hour.IntervalSeconds).Should().BeApproximately(3600, 1e-6, "the last tile is at the end");
    }

    [Fact]
    public void Each_Event_Gets_A_Tile_After_The_Grid_And_Its_Moment_Comes_In_Order()
    {
        var (previews, moments) = RecordingPreviewLayout.Plan(10, 640, 480, [2.5, 7.5, 11]);

        previews.Events.Should().Equal(new RecordingPreviewEvent(2.5, 11), new RecordingPreviewEvent(7.5, 12));
        moments.Should().HaveCount(13, "eleven grid tiles and two events; one past the end has none");
        moments.Select(m => m.Seconds).Should().BeInAscendingOrder();
        moments.Should().Contain((2.5, 11));
    }

    [Fact]
    public void The_Busiest_Second_Of_An_Event_Is_The_Most_Moving_Then_The_Most_Changed()
    {
        ActivityBucket[] buckets =
        [
            new(1, 0, IsAnalysed: true),
            new(10, 1, IsAnalysed: true),
            new(30, 2, IsAnalysed: true),
            new(50, 1, IsAnalysed: true),
            .. Enumerable.Repeat(new ActivityBucket(1, 0, IsAnalysed: true), 6),
            new(10, 1, IsAnalysed: true),
            new(20, 1, IsAnalysed: true),
        ];

        RecordingPreviewLayout.PeakSeconds(buckets, 12).Should().Equal(2.5, 11.5);
        RecordingPreviewLayout.PeakSeconds([], 7).Should().BeEmpty();
    }

    [Fact]
    public void A_Moment_Has_The_Nearest_Grid_Tile_And_An_Event_Its_Own()
    {
        var (previews, _) = RecordingPreviewLayout.Plan(10, 640, 480, [6.5]);

        RecordingPreviewLayout.GridTile(previews, 4.4).Should().Be(4);
        RecordingPreviewLayout.GridTile(previews, 4.6).Should().Be(5);
        RecordingPreviewLayout.GridTile(previews, 99).Should().Be(10, "kept on the grid");
        RecordingPreviewLayout.EventTile(previews, 6, 7).Should().Be(11);
        RecordingPreviewLayout.EventTile(previews, 1, 2).Should().BeNull();
    }

    [Fact]
    public void Tiles_Fill_The_Rows_Of_The_Picture()
    {
        var (previews, _) = RecordingPreviewLayout.Plan(30, 640, 480, []);

        RecordingPreviewLayout.TileRect(previews, 0).Should().Be((0, 0, 160, 120));
        RecordingPreviewLayout.TileRect(previews, 21).Should().Be((160, 120, 160, 120), "the second of the second row");
        RecordingPreviewLayout.SheetSize(previews).Should().Be((20 * 160, 2 * 120), "31 tiles on rows of 20");
    }

    [Fact]
    public void The_Index_Is_Read_Back_As_Written_And_Only_With_Its_Picture()
    {
        Directory.CreateDirectory(folder);
        var video = Path.Combine(folder, "Cam_20261020_090000_000.mp4");
        var (previews, _) = RecordingPreviewLayout.Plan(10, 640, 480, [6.5]);

        RecordingPreviewFile.Write(video, previews, path => File.WriteAllBytes(path, [1, 2, 3]));

        RecordingPreviewFile.Read(video).Should().BeEquivalentTo(previews);
        File.Exists(RecordingPathHelper.BuildPreviewImagePath(video) + ".tmp").Should().BeFalse();
        Directory.GetFiles(folder).Select(Path.GetFileName).Should().BeEquivalentTo(
            "Cam_20261020_090000_000.previews.jpg",
            "Cam_20261020_090000_000.previews.json");

        File.Delete(RecordingPathHelper.BuildPreviewImagePath(video));
        RecordingPreviewFile.Read(video).Should().BeNull("an index without its picture is no previews");
    }

    [Fact]
    public void A_Failed_Picture_Leaves_No_Index_Behind()
    {
        Directory.CreateDirectory(folder);
        var video = Path.Combine(folder, "Cam_20261020_090000_000.mp4");
        var (previews, _) = RecordingPreviewLayout.Plan(10, 640, 480, []);
        RecordingPreviewFile.Write(video, previews, path => File.WriteAllBytes(path, [1]));

        var write = () => RecordingPreviewFile.Write(video, previews, _ => throw new IOException("disk full"));

        write.Should().Throw<IOException>();
        File.Exists(RecordingPathHelper.BuildPreviewIndexPath(video)).Should().BeFalse("the old index went first");
        RecordingPreviewFile.Read(video).Should().BeNull();
    }

    [Fact]
    public void The_Previews_Belong_To_The_Recording()
    {
        var video = Path.Combine(folder, "Cam", "Cam_20261020_090000_000.mp4");

        RecordingPathHelper.GetCompanionPaths(video, folder).Should().Contain(
        [
            Path.Combine(folder, "Cam", "Cam_20261020_090000_000.previews.jpg"),
            Path.Combine(folder, "Cam", "Cam_20261020_090000_000.previews.json"),
        ]);
    }
}