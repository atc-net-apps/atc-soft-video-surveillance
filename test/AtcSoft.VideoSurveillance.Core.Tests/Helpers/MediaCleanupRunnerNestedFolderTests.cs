namespace AtcSoft.VideoSurveillance.Helpers;

/// <summary>
/// A camera's own recording folder may sit inside the global one. Its thumbnails then live in
/// its own <c>thumbnails</c> folder, and the cleanup scanning the global folder must read them
/// against the camera's folder, not against a video next to them.
/// </summary>
public sealed class MediaCleanupRunnerNestedFolderTests : IDisposable
{
    private static readonly HashSet<string> RecordingExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".mkv", ".mp4", ".avi" };

    private static readonly HashSet<string> EmptySkipSet =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly string global = Path.Combine(Path.GetTempPath(), "atcsoft-nested-" + Guid.NewGuid().ToString("N"));

    private string Own => Path.Combine(global, "front-door");

    public void Dispose()
    {
        if (Directory.Exists(global))
        {
            Directory.Delete(global, recursive: true);
        }
    }

    [Fact]
    public void A_Thumbnail_Of_A_Camera_Folder_Inside_The_Global_One_Is_Not_Taken_For_An_Orphan()
    {
        Touch(Path.Combine(Own, "Front", "Front_20260101_101500_000.mkv"));
        var thumbnail = Touch(Path.Combine(Own, "thumbnails", "Front", "Front_20260101_101500_000.png"));

        var result = MediaCleanupRunner.RemoveOrphanThumbnails(global, RecordingExtensions, EmptySkipSet, nestedRoots: [Own]);

        File.Exists(thumbnail).Should().BeTrue("its recording exists in the camera's folder");
        result.DeletedThumbnails.Should().BeEmpty();
    }

    [Fact]
    public void An_Orphan_Of_A_Camera_Folder_Inside_The_Global_One_Is_Removed()
    {
        var orphan = Touch(Path.Combine(Own, "thumbnails", "Front", "Front_20260101_101500_000.png"));

        MediaCleanupRunner.RemoveOrphanThumbnails(global, RecordingExtensions, EmptySkipSet, nestedRoots: [Own]);

        File.Exists(orphan).Should().BeFalse();
    }

    [Fact]
    public void An_Old_Recording_Of_A_Camera_Folder_Inside_The_Global_One_Takes_Its_Thumbnail_Along()
    {
        var video = Touch(Path.Combine(Own, "Front", "Front_20260101_101500_000.mkv"), DateTime.Now.AddDays(-40));
        var thumbnail = Touch(Path.Combine(Own, "thumbnails", "Front", "Front_20260101_101500_000.png"), DateTime.Now.AddDays(-40));

        MediaCleanupRunner.CleanDirectory(global, RecordingExtensions, DateTime.Now.AddDays(-30), EmptySkipSet, deleteCompanionThumbnail: true, nestedRoots: [Own]);

        File.Exists(video).Should().BeFalse();
        File.Exists(thumbnail).Should().BeFalse();
    }

    private static string Touch(
        string path,
        DateTime? lastWrite = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "12345");
        if (lastWrite is { } time)
        {
            File.SetLastWriteTime(path, time);
        }
        else
        {
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddHours(-1));
        }

        return path;
    }
}