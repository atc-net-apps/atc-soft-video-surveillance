namespace AtcSoft.VideoSurveillance.Helpers;

/// <summary>
/// The reader finds the volume of a folder that does not exist yet, and reads its free space.
/// </summary>
public sealed class FreeSpaceReaderTests
{
    [Fact]
    public void The_Free_Space_Of_A_Folder_Not_Made_Yet_Is_Read_From_Its_Volume()
    {
        var folder = Path.Combine(Path.GetTempPath(), "atcsoft-not-made-" + Guid.NewGuid().ToString("N"), "recordings");

        var volume = FreeSpaceReader.Default.GetVolume(folder);

        volume.Should().NotBeNullOrEmpty();
        Path.GetFullPath(folder).Should().StartWith(Path.TrimEndingDirectorySeparator(volume!));
        FreeSpaceReader.Default.GetFreeBytes(volume!).Should().BeGreaterThan(0);
    }

    [Fact]
    public void A_Volume_That_Is_Not_There_Cannot_Be_Read()
    {
        FreeSpaceReader.Default.GetFreeBytes(@"\\atcsoft-no-such-server-" + Guid.NewGuid().ToString("N")[..8] + @"\share\").Should().BeNull();
    }
}