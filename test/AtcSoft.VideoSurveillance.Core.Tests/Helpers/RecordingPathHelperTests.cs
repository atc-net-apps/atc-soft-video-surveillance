namespace AtcSoft.VideoSurveillance.Helpers;

public sealed class RecordingPathHelperTests : IDisposable
{
    private static readonly DateTime Timestamp = new(2026, 10, 12, 10, 15, 0, 123, DateTimeKind.Local);
    private static readonly Guid CameraId = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");

    private readonly string root = Path.Combine(Path.GetTempPath(), "atcsoft-recpath-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void GetCameraFolderName_Keeps_A_Valid_Name()
    {
        RecordingPathHelper.GetCameraFolderName("Front Door", CameraId).Should().Be("Front Door");
    }

    [Fact]
    public void GetCameraFolderName_Replaces_Invalid_Characters()
    {
        RecordingPathHelper.GetCameraFolderName("Gate/North:1", CameraId).Should().Be("Gate_North_1");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetCameraFolderName_Falls_Back_To_The_Camera_Id_For_An_Empty_Name(
        string? displayName)
    {
        RecordingPathHelper.GetCameraFolderName(displayName, CameraId).Should().Be("0f8fad5b");
    }

    [Theory]
    [InlineData("thumbnails")]
    [InlineData("Thumbnails")]
    public void GetCameraFolderName_Never_Returns_The_Thumbnails_Folder(
        string displayName)
    {
        RecordingPathHelper.GetCameraFolderName(displayName, CameraId).Should().Be(displayName + "_");
    }

    [Fact]
    public void BuildRecordingPath_Flat_Layout_Is_Camera_Folder_And_File()
    {
        var path = RecordingPathHelper.BuildRecordingPath(root, "Front Door", Timestamp, "mkv");

        path.Should().Be(Path.Combine(root, "Front Door", "Front Door_20261012_101500_123.mkv"));
    }

    [Fact]
    public void BuildRecordingPath_Daily_Layout_Adds_A_Day_Folder()
    {
        var path = RecordingPathHelper.BuildRecordingPath(
            root,
            "Front Door",
            Timestamp,
            "mkv",
            new RecordingLayoutOptions(UseDailySubfolders: true, SeparateThumbnailFolder: false));

        path.Should().Be(Path.Combine(root, "Front Door", "2026-10-12", "Front Door_20261012_101500_123.mkv"));
    }

    [Fact]
    public void BuildRecordingPath_Day_Folder_Follows_The_Local_Day_For_A_Utc_Timestamp()
    {
        var utc = new DateTime(2026, 10, 12, 12, 0, 0, DateTimeKind.Utc);

        var path = RecordingPathHelper.BuildRecordingPath(
            root,
            "Cam",
            utc,
            "mp4",
            new RecordingLayoutOptions(UseDailySubfolders: true, SeparateThumbnailFolder: false));

        var expectedDay = utc.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        path.Should().Be(Path.Combine(root, "Cam", expectedDay, "Cam_20261012_120000_000.mp4"));
    }

    [Theory]
    [InlineData(null, ".mp4")]
    [InlineData("", ".mp4")]
    [InlineData(".mkv", ".mkv")]
    [InlineData("mkv", ".mkv")]
    public void BuildRecordingPath_Normalizes_The_Format(
        string? format,
        string expectedExtension)
    {
        var path = RecordingPathHelper.BuildRecordingPath(root, "Cam", Timestamp, format);

        Path.GetExtension(path).Should().Be(expectedExtension);
    }

    [Fact]
    public void BuildThumbnailPath_Flat_Layout_Is_Next_To_The_Video()
    {
        var video = Path.Combine(root, "Cam", "Cam_20261012_101500_123.mkv");

        RecordingPathHelper.BuildThumbnailPath(video, root)
            .Should().Be(Path.Combine(root, "Cam", "Cam_20261012_101500_123.png"));
    }

    [Fact]
    public void BuildThumbnailPath_Separate_Folder_Mirrors_The_Video_Tree_Below_Thumbnails()
    {
        var layout = new RecordingLayoutOptions(UseDailySubfolders: true, SeparateThumbnailFolder: true);
        var video = RecordingPathHelper.BuildRecordingPath(root, "Cam", Timestamp, "mkv", layout);

        RecordingPathHelper.BuildThumbnailPath(video, root, layout)
            .Should().Be(Path.Combine(root, "thumbnails", "Cam", "2026-10-12", "Cam_20261012_101500_123.png"));
    }

    [Fact]
    public void BuildThumbnailPath_Separate_Folder_Without_Day_Folders()
    {
        var layout = new RecordingLayoutOptions(UseDailySubfolders: false, SeparateThumbnailFolder: true);
        var video = RecordingPathHelper.BuildRecordingPath(root, "Cam", Timestamp, "mkv", layout);

        RecordingPathHelper.BuildThumbnailPath(video, root, layout)
            .Should().Be(Path.Combine(root, "thumbnails", "Cam", "Cam_20261012_101500_123.png"));
    }

    [Fact]
    public void BuildThumbnailPath_Stays_Next_To_A_Video_Outside_The_Root()
    {
        var layout = new RecordingLayoutOptions(UseDailySubfolders: false, SeparateThumbnailFolder: true);
        var video = Path.Combine(Path.GetTempPath(), "elsewhere", "clip.mkv");

        RecordingPathHelper.BuildThumbnailPath(video, root, layout)
            .Should().Be(Path.ChangeExtension(video, ".png"));
    }

    [Fact]
    public void FindThumbnailPath_Finds_A_Thumbnail_Next_To_The_Video()
    {
        var video = Touch(Path.Combine(root, "Cam", "a.mkv"));
        var thumbnail = Touch(Path.Combine(root, "Cam", "a.png"));

        RecordingPathHelper.FindThumbnailPath(video, root).Should().Be(thumbnail);
    }

    [Fact]
    public void FindThumbnailPath_Finds_A_Thumbnail_In_The_Separate_Folder()
    {
        var video = Touch(Path.Combine(root, "Cam", "2026-10-12", "a.mkv"));
        var thumbnail = Touch(Path.Combine(root, "thumbnails", "Cam", "2026-10-12", "a.png"));

        RecordingPathHelper.FindThumbnailPath(video, root).Should().Be(thumbnail);
    }

    [Fact]
    public void FindThumbnailPath_Returns_Null_When_There_Is_None()
    {
        var video = Touch(Path.Combine(root, "Cam", "a.mkv"));

        RecordingPathHelper.FindThumbnailPath(video, root).Should().BeNull();
    }

    [Fact]
    public void GetThumbnailCandidates_Lists_Both_Layouts()
    {
        var video = Path.Combine(root, "Cam", "a.mkv");

        RecordingPathHelper.GetThumbnailCandidates(video, root).Should().Equal(
            Path.Combine(root, "thumbnails", "Cam", "a.png"),
            Path.Combine(root, "Cam", "a.png"));
    }

    [Theory]
    [InlineData("Cam", "a.mkv")]
    [InlineData("Cam", "2026-10-12", "a.mkv")]
    public void GetCameraFolderFromPath_Is_The_First_Folder_Below_The_Root(
        params string[] segments)
    {
        var file = Path.Combine([root, .. segments]);

        RecordingPathHelper.GetCameraFolderFromPath(file, root).Should().Be("Cam");
    }

    [Fact]
    public void GetCameraFolderFromPath_Returns_Null_For_A_File_Directly_In_The_Root()
    {
        RecordingPathHelper.GetCameraFolderFromPath(Path.Combine(root, "a.mkv"), root).Should().BeNull();
    }

    [Fact]
    public void GetCameraFolderFromPath_Returns_Null_For_A_File_Outside_The_Root()
    {
        var outside = Path.Combine(Path.GetTempPath(), "elsewhere", "Cam", "a.mkv");

        RecordingPathHelper.GetCameraFolderFromPath(outside, root).Should().BeNull();
    }

    [Fact]
    public void IsInThumbnailsFolder_Is_True_Only_Below_The_Thumbnails_Folder()
    {
        RecordingPathHelper.IsInThumbnailsFolder(Path.Combine(root, "thumbnails", "Cam", "a.png"), root).Should().BeTrue();
        RecordingPathHelper.IsInThumbnailsFolder(Path.Combine(root, "Cam", "a.png"), root).Should().BeFalse();
    }

    [Theory]
    [InlineData("Front Door_20261012_101500.mkv", 0)]
    [InlineData("Front Door_20261012_101500_123.mkv", 123)]
    [InlineData("Front Door_20261012_101500_123_2.mkv", 123)]
    [InlineData("Front Door_20261012_101500_2.mkv", 0)]
    [InlineData("Cam_With_Underscores_20261012_101500_123.mp4", 123)]
    public void TryParseTimestamp_Reads_The_Start_Time_From_The_File_Name(
        string fileName,
        int expectedMilliseconds)
    {
        RecordingPathHelper.TryParseTimestamp(fileName, out var timestamp).Should().BeTrue();

        timestamp.Should().Be(new DateTime(2026, 10, 12, 10, 15, 0, expectedMilliseconds, DateTimeKind.Unspecified));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("holiday.mkv")]
    [InlineData("Cam_20261399_101500.mkv")]
    public void TryParseTimestamp_Rejects_Names_Without_A_Valid_Timestamp(
        string? fileName)
    {
        RecordingPathHelper.TryParseTimestamp(fileName, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("Front Door/2026-10-12/a.mkv", "Front%20Door/2026-10-12/a.mkv")]
    [InlineData(@"Front Door\2026-10-12\a.mkv", "Front%20Door/2026-10-12/a.mkv")]
    [InlineData("Cam #1/a.mkv", "Cam%20%231/a.mkv")]
    [InlineData("50% Gate/a.mkv", "50%25%20Gate/a.mkv")]
    [InlineData("Q?/a&b+c.mkv", "Q%3F/a%26b%2Bc.mkv")]
    [InlineData("plain.mkv", "plain.mkv")]
    public void EscapePathSegments_Escapes_Each_Segment_And_Keeps_The_Separators(
        string relativePath,
        string expected)
    {
        RecordingPathHelper.EscapePathSegments(relativePath).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void EscapePathSegments_Of_Nothing_Is_Empty(string? relativePath)
    {
        RecordingPathHelper.EscapePathSegments(relativePath).Should().BeEmpty();
    }

    [Fact]
    public void IsBeingWritten_Is_True_While_A_Recorder_Has_The_File_Open()
    {
        var path = Touch(Path.Combine(root, "Cam", "live.mkv"));

        // A recorder keeps the file open for writing and lets others read it.
        using (new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite))
        {
            RecordingPathHelper.IsBeingWritten(path).Should().BeTrue();
        }
    }

    [Fact]
    public void IsBeingWritten_Is_False_For_A_Finished_Recording()
    {
        var path = Touch(Path.Combine(root, "Cam", "done.mkv"));

        RecordingPathHelper.IsBeingWritten(path).Should().BeFalse();
    }

    [Fact]
    public void IsBeingWritten_Is_False_For_A_Missing_File()
    {
        RecordingPathHelper.IsBeingWritten(Path.Combine(root, "Cam", "none.mkv")).Should().BeFalse();
    }

    [Theory]
    [InlineData(false, false, @"Camera 1\Camera 1_20261012_101500_123.mkv", @"Camera 1\Camera 1_20261012_101500_123.png")]
    [InlineData(true, false, @"Camera 1\2026-10-12\Camera 1_20261012_101500_123.mkv", @"Camera 1\2026-10-12\Camera 1_20261012_101500_123.png")]
    [InlineData(false, true, @"Camera 1\Camera 1_20261012_101500_123.mkv", @"thumbnails\Camera 1\Camera 1_20261012_101500_123.png")]
    [InlineData(true, true, @"Camera 1\2026-10-12\Camera 1_20261012_101500_123.mkv", @"thumbnails\Camera 1\2026-10-12\Camera 1_20261012_101500_123.png")]
    public void BuildExample_Shows_Where_A_Recording_And_Its_Thumbnail_Go(
        bool daily,
        bool separateThumbnails,
        string expectedVideo,
        string expectedThumbnail)
    {
        var (video, thumbnail) = RecordingPathHelper.BuildExample(
            "Camera 1",
            Timestamp,
            "mkv",
            new RecordingLayoutOptions(daily, separateThumbnails));

        video.Should().Be(expectedVideo);
        thumbnail.Should().Be(expectedThumbnail);
    }

    private static string Touch(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, string.Empty);
        return path;
    }
}