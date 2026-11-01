namespace AtcSoft.Video.Surveillance.Blazor.App.Logic;

/// <summary>
/// After a recording event the page asks only for the newest recordings and puts them in place:
/// the old part of the list stays as it is.
/// </summary>
public sealed class RecordingListPatchTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void The_Newest_Are_Asked_For_From_Two_Hours_Back()
        => RecordingListPatch.Since([Create("old", Now.AddDays(-1))], Now).Should().Be(Now.AddHours(-2));

    [Fact]
    public void A_Recording_Still_Running_From_Earlier_Is_Asked_For_Too()
        => RecordingListPatch.Since([Create("long", Now.AddHours(-5), inProgress: true)], Now).Should().Be(Now.AddHours(-5));

    [Fact]
    public void The_Newest_Part_Is_Replaced_And_The_Old_Part_Stays()
    {
        var since = Now.AddHours(-2);
        Recording[] current =
        [
            Create("running", Now.AddMinutes(-30), inProgress: true),
            Create("deleted elsewhere", Now.AddMinutes(-90)),
            Create("yesterday", Now.AddDays(-1)),
        ];
        Recording[] recent =
        [
            Create("next file", Now.AddMinutes(-1), inProgress: true),
            Create("running", Now.AddMinutes(-30)),
        ];

        var merged = RecordingListPatch.Merge(current, recent, since);

        merged.Select(r => r.CameraName).Should().Equal("next file", "running", "yesterday");
        merged.Single(r => r.CameraName == "running").InProgress.Should().BeFalse("the file has ended");
    }

    private static Recording Create(
        string name,
        DateTimeOffset startedAt,
        bool inProgress = false)
        => new(
            Id: Guid.NewGuid(),
            CameraId: Guid.NewGuid(),
            CameraName: name,
            FilePath: name + ".mp4",
            StartedAt: startedAt,
            Duration: string.Empty,
            FileSizeBytes: 1,
            HasThumbnail: false,
            InProgress: inProgress,
            ThumbnailPath: null);
}