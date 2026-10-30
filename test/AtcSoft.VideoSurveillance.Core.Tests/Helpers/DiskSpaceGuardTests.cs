namespace AtcSoft.VideoSurveillance.Helpers;

/// <summary>
/// One guard keeps free space on every volume that holds recordings: the global folder, each
/// camera's own folder and the snapshot folder. It deletes the oldest media on a volume that is
/// short, across all folders on it, and says per folder whether the volume is fine, was
/// reclaimed, is still short, or could not be read.
/// </summary>
public sealed class DiskSpaceGuardTests : IDisposable
{
    private const long MinFree = 1000;

    private static readonly string[] Videos = [".mp4", ".mkv"];
    private static readonly string[] Pictures = [".png", ".jpg"];

    private readonly string root = Path.Combine(Path.GetTempPath(), "atcsoft-guard-" + Guid.NewGuid().ToString("N"));
    private readonly FakeVolumes volumes = new();

    public DiskSpaceGuardTests()
    {
        Directory.CreateDirectory(root);
    }

    private string VolumeA => Path.Combine(root, "A");

    private string VolumeB => Path.Combine(root, "B");

    public void Dispose()
    {
        try
        {
            Directory.Delete(root, recursive: true);
        }
        catch (IOException)
        {
            // Best effort.
        }
    }

    [Fact]
    public void A_Volume_With_Enough_Space_Is_Left_Alone()
    {
        var global = Path.Combine(VolumeA, "recordings");
        var video = Write(global, @"Cam\old.mkv", 500, daysAgo: 10);
        volumes.Add(VolumeA, free: 5000);

        var result = Guard().Enforce(Request([global]));

        File.Exists(video).Should().BeTrue();
        result.StateOf(video).Should().Be(DiskSpaceVolumeState.Ok);
    }

    [Fact]
    public void Only_The_Short_Volume_Is_Reclaimed_And_Its_Oldest_Recordings_Go_First()
    {
        var global = Path.Combine(VolumeA, "recordings");
        var own = Path.Combine(VolumeB, "front-door");
        var oldOnA = Write(global, @"Cam\old.mkv", 600, daysAgo: 30);
        var oldestOnB = Write(own, @"Front\oldest.mkv", 600, daysAgo: 20);
        var newerOnB = Write(own, @"Front\newer.mkv", 600, daysAgo: 1);
        volumes.Add(VolumeA, free: 5000);
        volumes.Add(VolumeB, free: 500);

        var result = Guard().Enforce(Request([global, own]));

        File.Exists(oldOnA).Should().BeTrue("volume A has enough space");
        File.Exists(oldestOnB).Should().BeFalse();
        File.Exists(newerOnB).Should().BeTrue("deleting the oldest was enough");
        result.StateOf(own).Should().Be(DiskSpaceVolumeState.Reclaimed);
        result.StateOf(global).Should().Be(DiskSpaceVolumeState.Ok);
    }

    [Fact]
    public void On_A_Shared_Volume_The_Oldest_Recording_Of_Any_Folder_Goes_First()
    {
        var global = Path.Combine(VolumeA, "recordings");
        var own = Path.Combine(VolumeA, "front-door");
        var newerInGlobal = Write(global, @"Cam\newer.mkv", 600, daysAgo: 5);
        var olderInOwn = Write(own, @"Front\older.mkv", 600, daysAgo: 9);
        volumes.Add(VolumeA, free: 500);

        Guard().Enforce(Request([global, own]));

        File.Exists(olderInOwn).Should().BeFalse("it is the oldest on the volume, whichever folder it is in");
        File.Exists(newerInGlobal).Should().BeTrue();
    }

    [Fact]
    public void A_Volume_Without_Enough_To_Delete_Is_Still_Short()
    {
        var global = Path.Combine(VolumeA, "recordings");
        Write(global, @"Cam\only.mkv", 100, daysAgo: 3);
        volumes.Add(VolumeA, free: 100);

        var result = Guard().Enforce(Request([global]));

        result.StateOf(global).Should().Be(DiskSpaceVolumeState.StillShort);
        result.ShortFolders.Should().ContainSingle();
    }

    [Fact]
    public void A_Volume_Whose_Space_Cannot_Be_Read_Is_Reported_And_Nothing_Is_Deleted()
    {
        var share = Path.Combine(VolumeA, "share");
        var video = Write(share, @"Cam\old.mkv", 600, daysAgo: 30);
        volumes.Add(VolumeA, free: null);

        var result = Guard().Enforce(Request([share]));

        File.Exists(video).Should().BeTrue();
        result.StateOf(video).Should().Be(DiskSpaceVolumeState.Unreadable);
    }

    [Fact]
    public void A_Recording_Being_Written_Is_Never_Deleted()
    {
        var global = Path.Combine(VolumeA, "recordings");
        var active = Write(global, @"Cam\active.mkv", 600, daysAgo: 40);
        var old = Write(global, @"Cam\old.mkv", 600, daysAgo: 30);
        volumes.Add(VolumeA, free: 500);

        Guard().Enforce(Request([global], active: [active]));

        File.Exists(active).Should().BeTrue();
        File.Exists(old).Should().BeFalse();
    }

    [Fact]
    public void The_Thumbnail_Of_A_Camera_Folder_Inside_The_Global_Folder_Goes_With_Its_Recording()
    {
        // The camera's own folder sits inside the global one and keeps its thumbnails apart.
        var global = Path.Combine(VolumeA, "recordings");
        var own = Path.Combine(global, "front-door");
        var video = Write(own, @"Front\2026-10-01\old.mkv", 600, daysAgo: 17);
        var thumbnail = Write(own, @"thumbnails\Front\2026-10-01\old.png", 10, daysAgo: 17);
        volumes.Add(VolumeA, free: 500);

        Guard().Enforce(Request([global, own]));

        File.Exists(video).Should().BeFalse();
        File.Exists(thumbnail).Should().BeFalse("it belongs to the camera's own folder, not to a video next to it");
    }

    [Fact]
    public void Old_Snapshots_And_Time_Lapse_Pictures_Count_With_The_Recordings()
    {
        var global = Path.Combine(VolumeA, "recordings");
        var snapshots = Path.Combine(VolumeA, "snapshots");
        var oldFrame = Write(snapshots, @"timelapse\Cam\frame.png", 600, daysAgo: 50);
        var newerVideo = Write(global, @"Cam\newer.mkv", 600, daysAgo: 2);
        volumes.Add(VolumeA, free: 500);

        Guard().Enforce(Request([global], snapshots: snapshots));

        File.Exists(oldFrame).Should().BeFalse("it is the oldest media on the volume");
        File.Exists(newerVideo).Should().BeTrue();
    }

    [Fact]
    public void A_Recording_Thumbnail_Is_Not_Taken_For_A_Snapshot()
    {
        // The snapshot folder holds the recording folder: recording thumbnails are pictures too.
        var snapshots = Path.Combine(VolumeA, "media");
        var global = Path.Combine(snapshots, "recordings");
        var video = Write(global, @"Cam\video.mkv", 100, daysAgo: 1);
        var thumbnail = Write(global, @"Cam\video.png", 900, daysAgo: 60);
        volumes.Add(VolumeA, free: 500);

        Guard().Enforce(Request([global], snapshots: snapshots));

        File.Exists(thumbnail).Should().BeFalse("it went with its recording, the oldest video");
        File.Exists(video).Should().BeFalse();
        Directory.EnumerateFiles(snapshots, "*.png", SearchOption.AllDirectories).Should().BeEmpty();
    }

    [Fact]
    public void The_Thumbnails_Of_Recordings_Being_Written_Are_Protected_Too()
    {
        var global = Path.Combine(VolumeA, "recordings");
        var own = Path.Combine(global, "front-door");
        var active = Path.Combine(own, @"Front\live.mkv");

        var protectedPaths = DiskSpaceGuard.ProtectedPaths([active], [global, own]);

        protectedPaths.Should().Contain(Path.GetFullPath(active))
            .And.Contain(Path.GetFullPath(Path.Combine(own, @"thumbnails\Front\live.png")));
    }

    [Fact]
    public void Above_The_Size_Limit_The_Oldest_Recordings_Of_All_Folders_Go_Until_It_Fits()
    {
        var global = Path.Combine(VolumeA, "recordings");
        var own = Path.Combine(VolumeB, "front-door");
        var oldest = Write(own, @"Front\oldest.mkv", 400, daysAgo: 9);
        var oldestThumbnail = Write(own, @"Front\oldest.png", 10, daysAgo: 9);
        var middle = Write(global, @"Cam\middle.mkv", 400, daysAgo: 5);
        var newest = Write(global, @"Cam\newest.mkv", 400, daysAgo: 1);
        volumes.Add(VolumeA, free: 5000);
        volumes.Add(VolumeB, free: 5000);

        var run = Guard().EnforceSizeLimit([global, own], maxBytes: 900, DiskSpaceGuard.ProtectedPaths([], [global, own]), Videos);

        File.Exists(oldest).Should().BeFalse();
        File.Exists(oldestThumbnail).Should().BeFalse("it goes with its recording");
        File.Exists(middle).Should().BeTrue("800 bytes of recordings are left, within the 900 kept");
        File.Exists(newest).Should().BeTrue();
        run.DeletedFiles.Should().ContainSingle();
    }

    [Fact]
    public void A_Recording_Being_Written_Counts_Toward_The_Size_Limit_But_Stays()
    {
        var global = Path.Combine(VolumeA, "recordings");
        var active = Write(global, @"Cam\active.mkv", 800, daysAgo: 3);
        var old = Write(global, @"Cam\old.mkv", 400, daysAgo: 2);

        Guard().EnforceSizeLimit([global], maxBytes: 900, DiskSpaceGuard.ProtectedPaths([active], [global]), Videos);

        File.Exists(active).Should().BeTrue();
        File.Exists(old).Should().BeFalse();
    }

    [Fact]
    public void No_Size_Limit_Deletes_Nothing()
    {
        var global = Path.Combine(VolumeA, "recordings");
        var video = Write(global, @"Cam\old.mkv", 400, daysAgo: 300);

        Guard().EnforceSizeLimit([global], maxBytes: 0, DiskSpaceGuard.ProtectedPaths([], [global]), Videos);

        File.Exists(video).Should().BeTrue();
    }

    private static DiskSpaceGuardRequest Request(
        IReadOnlyList<string> recordingFolders,
        IReadOnlyList<string>? active = null,
        string? snapshots = null)
        => new(
            recordingFolders,
            snapshots,
            MinFree,
            DiskSpaceGuard.ProtectedPaths(active ?? [], recordingFolders),
            Videos,
            Pictures);

    private DiskSpaceGuard Guard()
        => new(volumes);

    private static string Write(
        string folder,
        string relativePath,
        int size,
        int daysAgo)
    {
        var path = Path.Combine(folder, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, new byte[size]);
        File.SetLastWriteTime(path, DateTime.Now.AddDays(-daysAgo));
        return path;
    }

    // Volumes are folders below the test folder; their free space is set by the test.
    private sealed class FakeVolumes : IFreeSpaceReader
    {
        private readonly Dictionary<string, long?> free = new(StringComparer.OrdinalIgnoreCase);

        public void Add(
            string volume,
            long? free)
            => this.free[Path.GetFullPath(volume)] = free;

        public string? GetVolume(string path)
            => free.Keys.FirstOrDefault(v => Path.GetFullPath(path).StartsWith(v + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                                             || string.Equals(Path.GetFullPath(path), v, StringComparison.OrdinalIgnoreCase));

        public long? GetFreeBytes(string volume)
            => free.GetValueOrDefault(volume);
    }
}