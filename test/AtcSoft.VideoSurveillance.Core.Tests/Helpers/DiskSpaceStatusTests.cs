namespace AtcSoft.VideoSurveillance.Helpers;

/// <summary>
/// How full the disk with the recordings is, for a status bar: recordings
/// stop when it runs out, so it should be seen coming.
/// </summary>
public sealed class DiskSpaceStatusTests
{
    private const long Gigabyte = 1024L * 1024 * 1024;
    private const long Megabyte = 1024L * 1024;

    [Theory]
    [InlineData(120 * Gigabyte, 500 * Gigabyte, "120 GB free of 500 GB")]
    [InlineData((long)(1.5 * Gigabyte), 64 * Gigabyte, "1.5 GB free of 64 GB")]
    [InlineData(800 * Megabyte, 64 * Gigabyte, "800 MB free of 64 GB")]
    [InlineData(0, 64 * Gigabyte, "0 MB free of 64 GB")]
    [InlineData(2500 * Gigabyte, 4000 * Gigabyte, "2.4 TB free of 3.9 TB")]
    public void The_Free_Space_Is_Told_In_The_Unit_That_Reads_Best(
        long freeBytes,
        long totalBytes,
        string expected)
        => new DiskSpaceStatus(freeBytes, totalBytes, MinFreeBytes: 0).Text.Should().Be(expected);

    [Theory]
    [InlineData(3000, 2048, false)]
    [InlineData(2048, 2048, false)]
    [InlineData(2047, 2048, true)]
    [InlineData(0, 2048, true)]
    [InlineData(10, 0, false)]
    public void The_Disk_Is_Low_Below_The_Free_Space_The_Recordings_Need(
        long freeMegabytes,
        long neededMegabytes,
        bool expectedLow)
        => new DiskSpaceStatus(freeMegabytes * Megabyte, 500 * Gigabyte, neededMegabytes * Megabyte)
            .IsLow.Should().Be(expectedLow);

    [Theory]
    [InlineData(125, 500, 75)]
    [InlineData(500, 500, 0)]
    [InlineData(0, 500, 100)]
    [InlineData(10, 0, 0)]
    public void The_Used_Share_Is_A_Whole_Percentage(
        long freeGigabytes,
        long totalGigabytes,
        int expectedPercent)
        => new DiskSpaceStatus(freeGigabytes * Gigabyte, totalGigabytes * Gigabyte, 0)
            .UsedPercent.Should().Be(expectedPercent);

    [Fact]
    public void The_Disk_Of_A_Folder_That_Exists_Is_Read()
    {
        var status = DiskSpaceStatus.Read(Path.GetTempPath(), minFreeSpaceMb: 100);

        status.Should().NotBeNull();
        status!.TotalBytes.Should().BeGreaterThan(0);
        status.FreeBytes.Should().BeInRange(0, status.TotalBytes);
        status.MinFreeBytes.Should().Be(100 * Megabyte);
    }

    [Fact]
    public void The_Disk_Of_A_Folder_That_Is_Not_There_Yet_Is_Read_Too()
    {
        // The recording folder is made with the first recording.
        var notYet = Path.Combine(Path.GetTempPath(), "atcsoft-not-yet-" + Guid.NewGuid().ToString("N"), "recordings");

        DiskSpaceStatus.Read(notYet, minFreeSpaceMb: 0).Should().NotBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Q:\\does-not-exist\\recordings")]
    [InlineData("\\\\no-such-server-atcsoft\\share\\recordings")]
    public void A_Path_Without_A_Readable_Disk_Gives_Nothing_Instead_Of_Failing(
        string? path)
        => DiskSpaceStatus.Read(path, minFreeSpaceMb: 0).Should().BeNull();

    [Fact]
    public void The_Guard_Switched_Off_Means_The_Disk_Is_Never_Called_Low()
    {
        var status = DiskSpaceStatus.Read(Path.GetTempPath(), minFreeSpaceMb: int.MaxValue, guardEnabled: false);

        status!.IsLow.Should().BeFalse();
        status.MinFreeBytes.Should().Be(0);
    }
}