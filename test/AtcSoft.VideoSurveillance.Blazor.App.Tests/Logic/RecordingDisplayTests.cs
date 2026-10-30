namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

public sealed class RecordingDisplayTests
{
    [Theory]
    [InlineData("PT5S", "0:05")]
    [InlineData("PT1M5S", "1:05")]
    [InlineData("PT15M", "15:00")]
    [InlineData("PT1H2M3S", "1:02:03")]
    [InlineData("PT1M5.75S", "1:05")]
    [InlineData("00:01:05", "1:05")]
    [InlineData("01:02:03.4560000", "1:02:03")]
    public void FormatDuration_Reads_Iso_And_Clock_Durations(
        string duration,
        string expected)
        => RecordingDisplay.FormatDuration(duration).Should().Be(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("soon")]
    [InlineData("PT0S")]
    public void FormatDuration_Shows_A_Dash_When_The_Length_Is_Not_Known(
        string? duration)
        => RecordingDisplay.FormatDuration(duration).Should().Be("–");

    [Theory]
    [InlineData(0L, "–")]
    [InlineData(512L, "512 B")]
    [InlineData(2048L, "2.0 KB")]
    [InlineData(5_242_880L, "5.0 MB")]
    [InlineData(1_610_612_736L, "1.50 GB")]
    public void FormatSize_Uses_The_Largest_Fitting_Unit(
        long bytes,
        string expected)
        => RecordingDisplay.FormatSize(bytes).Should().Be(expected);

    [Theory]
    [InlineData("Front Door/2026-10-12/Front Door_20261012_101500_000.mp4", "Front Door_20261012_101500_000.mp4")]
    [InlineData(@"C:\rec\Front Door\a.mkv", "a.mkv")]
    [InlineData("a.mkv", "a.mkv")]
    public void FileName_Is_The_Last_Part_Of_The_Path(
        string filePath,
        string expected)
        => RecordingDisplay.FileName(filePath).Should().Be(expected);

    [Theory]
    [InlineData("a.mp4", true)]
    [InlineData("a.MP4", true)]
    [InlineData("a.mkv", false)]
    [InlineData("a.avi", false)]
    [InlineData("a", false)]
    public void Only_Mp4_Is_Sure_To_Play_In_A_Browser(
        string filePath,
        bool expected)
        => RecordingDisplay.PlaysInBrowser(filePath).Should().Be(expected);

    [Fact]
    public void FileUrl_Escapes_Each_Part_Of_The_Path()
        => RecordingDisplay.FileUrl("http://server:5000", "Cam #1/2026-10-12/Cam #1_a.mp4")
            .Should().Be("http://server:5000/recordings-files/Cam%20%231/2026-10-12/Cam%20%231_a.mp4");

    [Fact]
    public void FileUrl_Accepts_A_Base_Address_With_A_Trailing_Slash()
        => RecordingDisplay.FileUrl("http://server:5000/", "a/b.png")
            .Should().Be("http://server:5000/recordings-files/a/b.png");

    [Fact]
    public void DownloadUrl_Asks_The_Server_To_Send_The_File_As_An_Attachment()
        => RecordingDisplay.DownloadUrl("http://server:5000", "Cam #1/a.mp4")
            .Should().Be("http://server:5000/recordings-files/Cam%20%231/a.mp4?download=1");

    [Fact]
    public void A_Signed_In_Browser_Adds_Its_Media_Token_To_The_Address()
    {
        RecordingDisplay.FileUrl("http://server:5000", "a/b.mp4", "token+/=")
            .Should().Be("http://server:5000/recordings-files/a/b.mp4?media=token%2B%2F%3D");
        RecordingDisplay.DownloadUrl("http://server:5000", "a/b.mp4", "token")
            .Should().Be("http://server:5000/recordings-files/a/b.mp4?media=token&download=1");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void FileUrl_Is_Null_Without_A_Path(string? path)
        => RecordingDisplay.FileUrl("http://server:5000", path).Should().BeNull();

    [Fact]
    public void FormatSize_Writes_The_Number_The_Chosen_Way()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("da-DK");

            RecordingDisplay.FormatSize(1_468_675).Should().Be("1,4 MB");
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Only_A_Finished_Recording_Without_An_Index_Can_Be_Analysed()
    {
        RecordingDisplay.CanAnalyse(Recording(activity: null, inProgress: false)).Should().BeTrue();
        RecordingDisplay.CanAnalyse(Recording(activity: [0, 5], inProgress: false)).Should().BeFalse("it has its index");
        RecordingDisplay.CanAnalyse(Recording(activity: null, inProgress: true)).Should().BeFalse("it gets its index as it is recorded");
    }

    private static Recording Recording(
        List<int>? activity,
        bool inProgress)
        => new(
            Id: Guid.NewGuid(),
            CameraId: Guid.NewGuid(),
            CameraName: "Gate",
            FilePath: "Gate/a.mkv",
            StartedAt: DateTimeOffset.UnixEpoch,
            Duration: "PT1M",
            FileSizeBytes: 1,
            HasThumbnail: false,
            InProgress: inProgress,
            ThumbnailPath: null,
            Activity: activity);
}