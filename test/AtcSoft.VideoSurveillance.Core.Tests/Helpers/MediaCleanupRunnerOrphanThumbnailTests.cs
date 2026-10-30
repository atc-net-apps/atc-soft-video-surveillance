namespace AtcSoft.VideoSurveillance.Helpers;

/// <summary>
/// A thumbnail whose recording is gone (typically deleted by hand in
/// Explorer) is an orphan: nothing lists it and nothing else removes it.
/// </summary>
public sealed class MediaCleanupRunnerOrphanThumbnailTests : IDisposable
{
    private static readonly HashSet<string> RecordingExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".mkv", ".mp4", ".avi" };

    private static readonly HashSet<string> EmptySkipSet =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly string root = Path.Combine(Path.GetTempPath(), "atcsoft-orphans-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Removes_A_Thumbnail_Next_To_A_Recording_That_Is_Gone()
    {
        var orphan = Touch(Path.Combine("Cam", "Cam_20260101_101500_000.png"));

        var result = MediaCleanupRunner.RemoveOrphanThumbnails(root, RecordingExtensions, EmptySkipSet);

        File.Exists(orphan).Should().BeFalse();
        result.DeletedThumbnails.Should().Equal(orphan);
        result.BytesFreed.Should().Be(5);
    }

    [Theory]
    [InlineData(".mkv")]
    [InlineData(".mp4")]
    [InlineData(".avi")]
    public void Keeps_A_Thumbnail_Whose_Recording_Is_Next_To_It(
        string extension)
    {
        Touch(Path.Combine("Cam", "a" + extension));
        var thumbnail = Touch(Path.Combine("Cam", "a.png"));

        var result = MediaCleanupRunner.RemoveOrphanThumbnails(root, RecordingExtensions, EmptySkipSet);

        File.Exists(thumbnail).Should().BeTrue();
        result.DeletedThumbnails.Should().BeEmpty();
    }

    [Fact]
    public void Removes_A_Thumbnail_In_The_Thumbnails_Folder_Whose_Recording_Is_Gone()
    {
        var orphan = Touch(Path.Combine("thumbnails", "Cam", "2026-01-01", "a.png"));

        var result = MediaCleanupRunner.RemoveOrphanThumbnails(root, RecordingExtensions, EmptySkipSet);

        File.Exists(orphan).Should().BeFalse();
        result.DeletedThumbnails.Should().Equal(orphan);
    }

    [Fact]
    public void Keeps_A_Thumbnail_In_The_Thumbnails_Folder_Whose_Recording_Exists()
    {
        Touch(Path.Combine("Cam", "2026-01-01", "a.mkv"));
        var thumbnail = Touch(Path.Combine("thumbnails", "Cam", "2026-01-01", "a.png"));

        var result = MediaCleanupRunner.RemoveOrphanThumbnails(root, RecordingExtensions, EmptySkipSet);

        File.Exists(thumbnail).Should().BeTrue();
        result.DeletedThumbnails.Should().BeEmpty();
    }

    [Fact]
    public void Keeps_A_Thumbnail_That_Is_Being_Written()
    {
        // A recording writes its thumbnail's first frames within seconds of
        // starting; a pass must not race it.
        var fresh = Touch(Path.Combine("Cam", "a.png"), age: TimeSpan.Zero);

        var result = MediaCleanupRunner.RemoveOrphanThumbnails(
            root,
            RecordingExtensions,
            EmptySkipSet,
            minimumAge: TimeSpan.FromMinutes(1));

        File.Exists(fresh).Should().BeTrue();
        result.DeletedThumbnails.Should().BeEmpty();
    }

    [Fact]
    public void Keeps_A_Thumbnail_On_The_Skip_List()
    {
        var active = Touch(Path.Combine("Cam", "a.png"));
        var skip = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { Path.GetFullPath(active) };

        var result = MediaCleanupRunner.RemoveOrphanThumbnails(root, RecordingExtensions, skip);

        File.Exists(active).Should().BeTrue();
        result.DeletedThumbnails.Should().BeEmpty();
    }

    [Fact]
    public void Leaves_Files_Directly_In_The_Root_Alone()
    {
        // Only camera folders and the thumbnails folder belong to the recorder.
        var stray = Touch("notes.png");

        MediaCleanupRunner.RemoveOrphanThumbnails(root, RecordingExtensions, EmptySkipSet);

        File.Exists(stray).Should().BeTrue();
    }

    [Fact]
    public void Leaves_Excluded_Folders_Alone()
    {
        // Snapshots are images without a recording. If the snapshot folder
        // was placed inside the recording folder, it must not be emptied.
        var snapshot = Touch(Path.Combine("snapshots", "Cam_20260101_101500.png"));
        var timelapse = Touch(Path.Combine("snapshots", "timelapse", "Cam", "a.png"));

        var result = MediaCleanupRunner.RemoveOrphanThumbnails(
            root,
            RecordingExtensions,
            EmptySkipSet,
            excludedDirectories: [Path.Combine(root, "snapshots")]);

        File.Exists(snapshot).Should().BeTrue();
        File.Exists(timelapse).Should().BeTrue();
        result.DeletedThumbnails.Should().BeEmpty();
    }

    [Fact]
    public void A_Missing_Root_Is_Not_An_Error()
    {
        var result = MediaCleanupRunner.RemoveOrphanThumbnails(root, RecordingExtensions, EmptySkipSet);

        result.DeletedThumbnails.Should().BeEmpty();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Removes_An_Activity_Index_Whose_Recording_Is_Gone_And_Keeps_One_Whose_Recording_Is_There()
    {
        var orphan = Touch(Path.Combine("Cam", "2026-01-01", "Cam_20260101_101500_000.activity.jsonl"));
        Touch(Path.Combine("Cam", "2026-01-01", "Cam_20260101_111500_000.mkv"));
        var kept = Touch(Path.Combine("Cam", "2026-01-01", "Cam_20260101_111500_000.activity.jsonl"));

        var result = MediaCleanupRunner.RemoveOrphanThumbnails(root, RecordingExtensions, EmptySkipSet);

        File.Exists(orphan).Should().BeFalse();
        File.Exists(kept).Should().BeTrue();
        result.DeletedThumbnails.Should().Equal(orphan);
    }

    [Fact]
    public void Removes_Previews_Whose_Recording_Is_Gone_And_Keeps_Those_Whose_Recording_Is_There()
    {
        var orphans = new[] { ".previews.jpg", ".previews.json" }.Select(e => Touch(Path.Combine("Cam", "Cam_20260101_101500_000" + e))).ToList();
        Touch(Path.Combine("Cam", "Cam_20260101_111500_000.mp4"));
        var kept = Touch(Path.Combine("Cam", "Cam_20260101_111500_000.previews.jpg"));

        var result = MediaCleanupRunner.RemoveOrphanThumbnails(root, RecordingExtensions, EmptySkipSet);

        orphans.Should().OnlyContain(o => !File.Exists(o));
        File.Exists(kept).Should().BeTrue();
        result.DeletedThumbnails.Should().BeEquivalentTo(orphans);
    }

    [Fact]
    public void Leaves_A_Young_Activity_Index_And_One_Directly_In_The_Root_Alone()
    {
        var young = Touch(Path.Combine("Cam", "Cam_20260101_101500_000.activity.jsonl"), TimeSpan.FromSeconds(5));
        var inRoot = Touch("notes.activity.jsonl");

        var result = MediaCleanupRunner.RemoveOrphanThumbnails(root, RecordingExtensions, EmptySkipSet, minimumAge: TimeSpan.FromMinutes(10));

        File.Exists(young).Should().BeTrue("its recording may just have started");
        File.Exists(inRoot).Should().BeTrue();
        result.DeletedThumbnails.Should().BeEmpty();
    }

    private string Touch(
        string relativePath,
        TimeSpan? age = null)
    {
        var path = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "thumb");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow - (age ?? TimeSpan.FromHours(1)));
        return path;
    }
}