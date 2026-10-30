namespace AtcSoft.VideoSurveillance.Helpers;

public sealed class MediaCleanupRunnerReclaimTests : IDisposable
{
    private static readonly HashSet<string> RecordingExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".mkv", ".mp4" };

    private static readonly HashSet<string> EmptySkipSet =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly string tempDir;

    public MediaCleanupRunnerReclaimTests()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "MediaCleanupRunnerReclaim_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
        catch
        {
            // best effort
        }
    }

    [Fact]
    public void ReclaimBySize_TargetAlreadyMet_DeletesNothing()
    {
        // Arrange — target = 0, drive always has ≥ 0 bytes free
        WriteFile("old.mkv", "data", DateTime.Now.AddDays(-10));

        // Act
        var result = MediaCleanupRunner.ReclaimBySize(
            tempDir,
            RecordingExtensions,
            targetFreeBytes: 0,
            EmptySkipSet,
            deleteCompanionThumbnail: false);

        // Assert
        result.DeletedFiles.Should().BeEmpty();
        result.StillShort.Should().BeFalse();
        result.BytesFreed.Should().Be(0);
    }

    [Fact]
    public void ReclaimBySize_HandlesEmptyDirectory_ReturnsEmptyResult()
    {
        // Arrange — empty dir with large target
        var emptyDir = Path.Combine(tempDir, "empty_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(emptyDir);

        // Act
        var result = MediaCleanupRunner.ReclaimBySize(
            emptyDir,
            RecordingExtensions,
            targetFreeBytes: long.MaxValue,
            EmptySkipSet,
            deleteCompanionThumbnail: false);

        // Assert — exhausted all (zero) files, so StillShort=true (can't reach target)
        result.DeletedFiles.Should().BeEmpty();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ReclaimBySize_DirectoryMissing_ReturnsEmptyResultWithoutErrors()
    {
        // Arrange
        var missing = Path.Combine(tempDir, "does-not-exist");

        // Act
        var result = MediaCleanupRunner.ReclaimBySize(
            missing,
            RecordingExtensions,
            targetFreeBytes: long.MaxValue,
            EmptySkipSet,
            deleteCompanionThumbnail: false);

        // Assert
        result.DeletedFiles.Should().BeEmpty();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ReclaimBySize_ReclaimsOldestFirst()
    {
        // Arrange — three files, need to reclaim all (use MaxValue to force all)
        var oldest = WriteFile("cam_oldest.mkv", "aaa", DateTime.Now.AddDays(-10));
        var middle = WriteFile("cam_middle.mkv", "bbb", DateTime.Now.AddDays(-5));
        var newest = WriteFile("cam_newest.mkv", "ccc", DateTime.Now.AddDays(-1));

        // Act
        var result = MediaCleanupRunner.ReclaimBySize(
            tempDir,
            RecordingExtensions,
            targetFreeBytes: long.MaxValue,
            EmptySkipSet,
            deleteCompanionThumbnail: false);

        // Assert — all three deleted, oldest first
        result.DeletedFiles.Should().HaveCount(3);
        result.DeletedFiles[0].Should().Be(oldest);
        result.DeletedFiles[1].Should().Be(middle);
        result.DeletedFiles[2].Should().Be(newest);
    }

    [Fact]
    public void ReclaimBySize_StopsWhenTargetMet()
    {
        // Arrange — three files of 100 bytes each.
        // We need just 100 bytes freed (one file) to meet the target.
        const string payload = "1234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890"; // 100 bytes
        var oldest = WriteFile("cam_oldest.mkv", payload, DateTime.Now.AddDays(-10));
        var middle = WriteFile("cam_middle.mkv", payload, DateTime.Now.AddDays(-5));
        var newest = WriteFile("cam_newest.mkv", payload, DateTime.Now.AddDays(-1));

        // The free space is given, not read from the disk the tests run
        // on: everything else on the machine writes to and deletes from
        // that disk while the test runs.
        const long initialFree = 5_000;
        const long targetFreeBytes = initialFree + 100; // need to free exactly 100 bytes

        // Act
        var result = MediaCleanupRunner.ReclaimBySize(
            tempDir,
            RecordingExtensions,
            targetFreeBytes,
            EmptySkipSet,
            deleteCompanionThumbnail: false,
            _ => initialFree);

        // Assert — only the oldest file deleted; middle and newest preserved
        result.DeletedFiles.Should().ContainSingle();
        result.DeletedFiles[0].Should().Be(oldest);
        File.Exists(oldest).Should().BeFalse();
        File.Exists(middle).Should().BeTrue();
        File.Exists(newest).Should().BeTrue();
        result.StillShort.Should().BeFalse();
    }

    [Fact]
    public void ReclaimBySize_SkipsActiveRecordingPaths()
    {
        // Arrange — two files; the older one is "active" (in skip set)
        var active = WriteFile("cam_active.mkv", "active", DateTime.Now.AddDays(-20));
        var eligible = WriteFile("cam_eligible.mkv", "eligible", DateTime.Now.AddDays(-10));

        var skipPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.GetFullPath(active),
        };

        // Act — force all deletions
        var result = MediaCleanupRunner.ReclaimBySize(
            tempDir,
            RecordingExtensions,
            targetFreeBytes: long.MaxValue,
            skipPaths,
            deleteCompanionThumbnail: false);

        // Assert — active file skipped; eligible file deleted
        File.Exists(active).Should().BeTrue();
        File.Exists(eligible).Should().BeFalse();
        result.DeletedFiles.Should().ContainSingle();
        result.DeletedFiles[0].Should().Be(eligible);
        result.StillShort.Should().BeTrue(); // couldn't reach MaxValue
    }

    [Fact]
    public void ReclaimBySize_DeletesCompanionThumbnail()
    {
        // Arrange — mkv with a .png sibling
        var mkv = WriteFile("cam_old.mkv", "video-content", DateTime.Now.AddDays(-10));
        var png = Path.ChangeExtension(mkv, ".png");
        File.WriteAllText(png, "thumb");
        File.SetLastWriteTime(png, DateTime.Now.AddDays(-10));

        // Act
        var result = MediaCleanupRunner.ReclaimBySize(
            tempDir,
            RecordingExtensions,
            targetFreeBytes: long.MaxValue,
            EmptySkipSet,
            deleteCompanionThumbnail: true);

        // Assert
        File.Exists(mkv).Should().BeFalse();
        File.Exists(png).Should().BeFalse();
        result.DeletedFiles.Should().Contain(mkv);
        result.DeletedThumbnails.Should().Contain(png);
        result.BytesFreed.Should().Be("video-content".Length + "thumb".Length);
    }

    [Fact]
    public void ReclaimBySize_AllFilesSkipped_ReturnsStillShort()
    {
        // Arrange — two files, both active (in skip set)
        var file1 = WriteFile("cam1.mkv", "aaa", DateTime.Now.AddDays(-10));
        var file2 = WriteFile("cam2.mkv", "bbb", DateTime.Now.AddDays(-5));

        var skipPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.GetFullPath(file1),
            Path.GetFullPath(file2),
        };

        // One byte short. The free space is given, not read from the disk
        // the tests run on: a byte freed there by anything else between
        // reading it here and reading it in the method made the target
        // "already met" and the test fail now and then.
        const long freeBytes = 5_000;

        // Act
        var result = MediaCleanupRunner.ReclaimBySize(
            tempDir,
            RecordingExtensions,
            targetFreeBytes: freeBytes + 1,
            skipPaths,
            deleteCompanionThumbnail: false,
            _ => freeBytes);

        // Assert — nothing deleted; couldn't reach target
        result.DeletedFiles.Should().BeEmpty();
        result.BytesFreed.Should().Be(0);
        result.StillShort.Should().BeTrue();
    }

    [Fact]
    public void ReclaimBySize_ReadsTheFreeSpaceOfTheDriveTheFolderIsOn()
    {
        string? asked = null;

        MediaCleanupRunner.ReclaimBySize(
            tempDir,
            RecordingExtensions,
            targetFreeBytes: 1,
            EmptySkipSet,
            deleteCompanionThumbnail: false,
            driveRoot =>
            {
                asked = driveRoot;
                return 0;
            });

        asked.Should().Be(Path.GetPathRoot(Path.GetFullPath(tempDir)));
    }

    [Fact]
    public void ReclaimBySize_FreeSpaceThatCannotBeRead_IsReportedAndNothingIsDeleted()
    {
        var file = WriteFile("cam.mkv", "aaa", DateTime.Now.AddDays(-10));

        var result = MediaCleanupRunner.ReclaimBySize(
            tempDir,
            RecordingExtensions,
            targetFreeBytes: long.MaxValue,
            EmptySkipSet,
            deleteCompanionThumbnail: false,
            _ => throw new IOException("The drive is not ready."));

        result.Errors.Should().ContainSingle();
        result.DeletedFiles.Should().BeEmpty();
        File.Exists(file).Should().BeTrue();
    }

    private string WriteFile(
        string relativePath,
        string contents,
        DateTime lastWrite)
    {
        var path = Path.Combine(tempDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
        File.SetLastWriteTime(path, lastWrite);
        return path;
    }
}