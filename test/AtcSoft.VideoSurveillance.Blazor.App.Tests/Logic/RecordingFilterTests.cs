namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

public sealed class RecordingFilterTests
{
    private static readonly Guid Front = Guid.NewGuid();
    private static readonly Guid Garden = Guid.NewGuid();

    // Noon on 13 October 2026, in a zone two hours ahead of UTC.
    private static readonly TimeSpan Offset = TimeSpan.FromHours(2);
    private static readonly DateTimeOffset Now = new(2026, 10, 13, 12, 0, 0, Offset);

    private static readonly Recording[] Recordings =
    [
        Create(Front, "today-morning", new DateTimeOffset(2026, 10, 13, 8, 0, 0, Offset)),
        Create(Garden, "today-just-after-midnight", new DateTimeOffset(2026, 10, 13, 0, 30, 0, Offset)),
        Create(Front, "yesterday-late", new DateTimeOffset(2026, 10, 12, 23, 30, 0, Offset)),
        Create(Garden, "six-days-ago", new DateTimeOffset(2026, 10, 7, 9, 0, 0, Offset)),
        Create(Front, "seven-days-ago", new DateTimeOffset(2026, 10, 6, 9, 0, 0, Offset)),
        Create(Garden, "last-month", new DateTimeOffset(2026, 9, 1, 9, 0, 0, Offset)),
    ];

    [Fact]
    public void No_Filter_Shows_Everything()
        => Names(new RecordingFilter()).Should().HaveCount(6);

    [Fact]
    public void A_Camera_Filter_Shows_That_Camera_Only()
        => Names(new RecordingFilter { CameraId = Garden })
            .Should().Equal("today-just-after-midnight", "six-days-ago", "last-month");

    [Fact]
    public void Today_Is_The_Viewers_Day_Not_The_Utc_Day()
    {
        // 00:30 local is still the 12th in UTC; the viewer calls it today.
        Names(new RecordingFilter { Day = RecordingDayFilter.Today })
            .Should().Equal("today-morning", "today-just-after-midnight");
    }

    [Fact]
    public void Yesterday_Shows_The_Day_Before()
        => Names(new RecordingFilter { Day = RecordingDayFilter.Yesterday })
            .Should().Equal("yesterday-late");

    [Fact]
    public void Last_Seven_Days_Includes_Today_And_The_Six_Days_Before()
        => Names(new RecordingFilter { Day = RecordingDayFilter.Last7Days })
            .Should().Equal("today-morning", "today-just-after-midnight", "yesterday-late", "six-days-ago");

    [Fact]
    public void Last_Thirty_Days_Leaves_Out_Older_Recordings()
        => Names(new RecordingFilter { Day = RecordingDayFilter.Last30Days })
            .Should().NotContain("last-month").And.HaveCount(5);

    [Fact]
    public void Filters_Combine()
        => Names(new RecordingFilter { CameraId = Front, Day = RecordingDayFilter.Today })
            .Should().Equal("today-morning");

    private static List<string> Names(RecordingFilter filter)
        => [.. filter.Apply(Recordings, Now).Select(r => r.CameraName!)];

    private static Recording Create(
        Guid cameraId,
        string name,
        DateTimeOffset startedAt)
        => new(
            Id: Guid.NewGuid(),
            CameraId: cameraId,
            CameraName: name,
            FilePath: name + ".mp4",
            StartedAt: startedAt.ToUniversalTime(),
            Duration: string.Empty,
            FileSizeBytes: 1,
            HasThumbnail: false,
            InProgress: false,
            ThumbnailPath: null);
}