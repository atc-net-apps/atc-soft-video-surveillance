namespace AtcSoft.VideoSurveillance.Helpers;

/// <summary>
/// Changing the folder layout settings moves the existing recordings and
/// thumbnails, so the folder always looks the way the settings say.
/// </summary>
public sealed class RecordingLayoutReorganizerTests : IDisposable
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase) { ".mkv", ".mp4" };
    private static readonly RecordingLayoutOptions Flat = new(UseDailySubfolders: false, SeparateThumbnailFolder: false);
    private static readonly RecordingLayoutOptions Daily = new(UseDailySubfolders: true, SeparateThumbnailFolder: false);
    private static readonly RecordingLayoutOptions SeparateThumbnails = new(UseDailySubfolders: false, SeparateThumbnailFolder: true);
    private static readonly RecordingLayoutOptions DailyAndSeparate = new(UseDailySubfolders: true, SeparateThumbnailFolder: true);

    private readonly string root = Path.Combine(Path.GetTempPath(), "atcsoft-reorganize-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Flat_To_Daily_Moves_Each_Recording_And_Its_Thumbnail_Into_Its_Day()
    {
        Touch("Cam", "Cam_20261012_101500_000.mkv");
        Touch("Cam", "Cam_20261012_101500_000.png");
        Touch("Cam", "Cam_20261013_080000_000.mkv");

        var result = RecordingLayoutReorganizer.Reorganize(root, Daily, Extensions);

        Files().Should().BeEquivalentTo(
            @"Cam\2026-10-12\Cam_20261012_101500_000.mkv",
            @"Cam\2026-10-12\Cam_20261012_101500_000.png",
            @"Cam\2026-10-13\Cam_20261013_080000_000.mkv");
        result.MovedRecordings.Should().Be(2);
        result.MovedThumbnails.Should().Be(1);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void The_Activity_Index_Moves_With_Its_Recording_And_Stays_Beside_It()
    {
        Touch("Cam", "Cam_20261012_101500_000.mkv");
        Touch("Cam", "Cam_20261012_101500_000.activity.jsonl");
        Touch("Cam", "Cam_20261012_101500_000.previews.jpg");
        Touch("Cam", "Cam_20261012_101500_000.previews.json");

        var result = RecordingLayoutReorganizer.Reorganize(root, DailyAndSeparate, Extensions);

        Files().Should().BeEquivalentTo(
            @"Cam\2026-10-12\Cam_20261012_101500_000.mkv",
            @"Cam\2026-10-12\Cam_20261012_101500_000.activity.jsonl",
            @"Cam\2026-10-12\Cam_20261012_101500_000.previews.jpg",
            @"Cam\2026-10-12\Cam_20261012_101500_000.previews.json");
        result.MovedRecordings.Should().Be(1, "the index is not a recording of its own");
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Flat_To_Daily_And_Separate_Moves_Thumbnails_To_The_Thumbnails_Folder()
    {
        Touch("Cam", "Cam_20261012_101500_000.mkv");
        Touch("Cam", "Cam_20261012_101500_000.png");

        RecordingLayoutReorganizer.Reorganize(root, DailyAndSeparate, Extensions);

        Files().Should().BeEquivalentTo(
            @"Cam\2026-10-12\Cam_20261012_101500_000.mkv",
            @"thumbnails\Cam\2026-10-12\Cam_20261012_101500_000.png");
    }

    [Fact]
    public void Flat_To_Separate_Thumbnails_Leaves_The_Recordings_Where_They_Are()
    {
        Touch("Cam", "Cam_20261012_101500_000.mkv");
        Touch("Cam", "Cam_20261012_101500_000.png");

        var result = RecordingLayoutReorganizer.Reorganize(root, SeparateThumbnails, Extensions);

        Files().Should().BeEquivalentTo(
            @"Cam\Cam_20261012_101500_000.mkv",
            @"thumbnails\Cam\Cam_20261012_101500_000.png");
        result.MovedRecordings.Should().Be(0);
        result.MovedThumbnails.Should().Be(1);
    }

    [Fact]
    public void Daily_And_Separate_Back_To_Flat_Puts_Everything_In_The_Camera_Folder()
    {
        Touch("Cam", "2026-10-12", "Cam_20261012_101500_000.mkv");
        Touch("thumbnails", "Cam", "2026-10-12", "Cam_20261012_101500_000.png");

        RecordingLayoutReorganizer.Reorganize(root, Flat, Extensions);

        Files().Should().BeEquivalentTo(
            @"Cam\Cam_20261012_101500_000.mkv",
            @"Cam\Cam_20261012_101500_000.png");
        Directory.Exists(Path.Combine(root, "thumbnails")).Should().BeFalse("emptied folders are removed");
        Directory.Exists(Path.Combine(root, "Cam", "2026-10-12")).Should().BeFalse("emptied folders are removed");
    }

    [Fact]
    public void A_Folder_That_Already_Matches_Is_Left_Untouched()
    {
        Touch("Cam", "2026-10-12", "Cam_20261012_101500_000.mkv");
        Touch("thumbnails", "Cam", "2026-10-12", "Cam_20261012_101500_000.png");

        var result = RecordingLayoutReorganizer.Reorganize(root, DailyAndSeparate, Extensions);

        result.MovedRecordings.Should().Be(0);
        result.MovedThumbnails.Should().Be(0);
        Files().Should().HaveCount(2);
    }

    [Fact]
    public void A_Mixed_Folder_Ends_Up_In_One_Layout()
    {
        Touch("Cam", "Cam_20261011_101500_000.mkv");
        Touch("Cam", "2026-10-12", "Cam_20261012_101500_000.mkv");
        Touch("Cam", "2026-10-12", "Cam_20261012_101500_000.png");

        RecordingLayoutReorganizer.Reorganize(root, DailyAndSeparate, Extensions);

        Files().Should().BeEquivalentTo(
            @"Cam\2026-10-11\Cam_20261011_101500_000.mkv",
            @"Cam\2026-10-12\Cam_20261012_101500_000.mkv",
            @"thumbnails\Cam\2026-10-12\Cam_20261012_101500_000.png");
    }

    [Fact]
    public void A_Recording_That_Is_Being_Written_Is_Not_Moved()
    {
        var live = Touch("Cam", "Cam_20261012_101500_000.mkv");
        var liveThumbnail = Touch("Cam", "Cam_20261012_101500_000.png");

        using (new FileStream(live, FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
        {
            var result = RecordingLayoutReorganizer.Reorganize(root, DailyAndSeparate, Extensions);

            result.MovedRecordings.Should().Be(0);
            result.MovedThumbnails.Should().Be(0);
        }

        File.Exists(live).Should().BeTrue();
        File.Exists(liveThumbnail).Should().BeTrue("the thumbnail stays with its recording");
    }

    [Fact]
    public void A_Utc_File_Name_Goes_Into_The_Local_Day()
    {
        // The server names files in UTC but its day folders follow the local day.
        var utc = new DateTime(2026, 10, 12, 23, 30, 0, DateTimeKind.Utc);
        var expectedDay = utc.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        Touch("Cam", "Cam_20261012_233000_000.mkv");

        RecordingLayoutReorganizer.Reorganize(root, Daily, Extensions, fileNamesAreUtc: true);

        Files().Should().BeEquivalentTo($@"Cam\{expectedDay}\Cam_20261012_233000_000.mkv");
    }

    [Fact]
    public void A_Recording_Without_A_Timestamp_In_Its_Name_Uses_The_File_Date()
    {
        var path = Touch("Cam", "holiday.mkv");
        File.SetLastWriteTime(path, new DateTime(2026, 7, 1, 12, 0, 0, DateTimeKind.Local));

        RecordingLayoutReorganizer.Reorganize(root, Daily, Extensions);

        Files().Should().BeEquivalentTo(@"Cam\2026-07-01\holiday.mkv");
    }

    [Fact]
    public void Files_That_Are_Not_Recordings_Are_Left_Alone()
    {
        Touch("notes.txt");
        Touch("Cam", "readme.txt");

        RecordingLayoutReorganizer.Reorganize(root, DailyAndSeparate, Extensions);

        Files().Should().BeEquivalentTo("notes.txt", @"Cam\readme.txt");
    }

    [Fact]
    public void A_Name_That_Is_Already_Taken_Is_Reported_And_Nothing_Is_Overwritten()
    {
        Touch("Cam", "Cam_20261012_101500_000.mkv");
        var existing = Touch("Cam", "2026-10-12", "Cam_20261012_101500_000.mkv");
        File.WriteAllText(existing, "the one already there");

        var result = RecordingLayoutReorganizer.Reorganize(root, Daily, Extensions);

        File.ReadAllText(existing).Should().Be("the one already there");
        File.Exists(Path.Combine(root, "Cam", "Cam_20261012_101500_000.mkv")).Should().BeTrue();
        result.Errors.Should().ContainSingle();
    }

    [Fact]
    public void A_Missing_Root_Is_Not_An_Error()
    {
        var result = RecordingLayoutReorganizer.Reorganize(root, DailyAndSeparate, Extensions);

        result.MovedRecordings.Should().Be(0);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void Synchronize_Restarts_A_Running_Recording_Whose_File_Would_Move()
    {
        // A running recording cannot be moved, so it is restarted: the
        // recorder closes the file and continues in a new one at the new
        // place, and the closed file can then be moved like any other.
        var live = Touch("Cam", "Cam_20261012_101500_000.mkv");
        var session = new RecordingSession(Guid.NewGuid(), "Cam", live, isManualRecording: true);
        var restarted = new List<Guid>();

        RecordingLayoutReorganizer.Synchronize(
            root,
            Daily,
            Extensions,
            [session],
            cameraId =>
            {
                restarted.Add(cameraId);
                return true;
            });

        restarted.Should().Equal(session.CameraId);
        Files().Should().BeEquivalentTo(@"Cam\2026-10-12\Cam_20261012_101500_000.mkv");
    }

    [Fact]
    public void Synchronize_Leaves_A_Running_Recording_Alone_When_It_Is_Already_In_Place()
    {
        var live = Touch("Cam", "2026-10-12", "Cam_20261012_101500_000.mkv");
        Touch("thumbnails", "Cam", "2026-10-12", "Cam_20261012_101500_000.png");
        var session = new RecordingSession(Guid.NewGuid(), "Cam", live, isManualRecording: true);
        var restarted = new List<Guid>();

        RecordingLayoutReorganizer.Synchronize(
            root,
            DailyAndSeparate,
            Extensions,
            [session],
            cameraId =>
            {
                restarted.Add(cameraId);
                return true;
            });

        restarted.Should().BeEmpty();
    }

    [Fact]
    public void Synchronize_Restarts_A_Running_Recording_When_Only_Its_Thumbnail_Would_Move()
    {
        var live = Touch("Cam", "Cam_20261012_101500_000.mkv");
        Touch("Cam", "Cam_20261012_101500_000.png");
        var session = new RecordingSession(Guid.NewGuid(), "Cam", live, isManualRecording: true);
        var restarted = new List<Guid>();

        RecordingLayoutReorganizer.Synchronize(
            root,
            SeparateThumbnails,
            Extensions,
            [session],
            cameraId =>
            {
                restarted.Add(cameraId);
                return true;
            });

        restarted.Should().Equal(session.CameraId);
        Files().Should().BeEquivalentTo(
            @"Cam\Cam_20261012_101500_000.mkv",
            @"thumbnails\Cam\Cam_20261012_101500_000.png");
    }

    [Fact]
    public void Synchronize_Ignores_A_Running_Recording_Outside_The_Root()
    {
        // A camera with its own recording folder is not part of this root.
        var elsewhere = Path.Combine(Path.GetTempPath(), "atcsoft-elsewhere", "Cam", "Cam_20261012_101500_000.mkv");
        var session = new RecordingSession(Guid.NewGuid(), "Cam", elsewhere, isManualRecording: true);
        var restarted = new List<Guid>();

        RecordingLayoutReorganizer.Synchronize(
            root,
            Daily,
            Extensions,
            [session],
            cameraId =>
            {
                restarted.Add(cameraId);
                return true;
            });

        restarted.Should().BeEmpty();
    }

    private string Touch(params string[] segments)
    {
        var path = Path.Combine([root, .. segments]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "x");
        return path;
    }

    private List<string> Files()
        => Directory.Exists(root)
            ? [.. Directory
                .EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .Select(f => Path.GetRelativePath(root, f))]
            : [];
}