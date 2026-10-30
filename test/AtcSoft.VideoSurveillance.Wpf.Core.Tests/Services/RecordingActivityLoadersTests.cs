namespace AtcSoft.VideoSurveillance.Wpf.Core.Services;

/// <summary>
/// CameraWall's player reads the activity index written beside the recording.
/// </summary>
public sealed class RecordingActivityLoadersTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 10, 19, 10, 15, 0, TimeSpan.Zero);

    private readonly string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "atcsoft-activity-loader-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
        => TestFolders.Delete(folder);

    [Fact]
    public async Task The_Index_Beside_The_Recording_Is_Shrunk_To_The_Stretches_Asked_For()
    {
        var video = System.IO.Path.Combine(folder, "Garden", "Garden_20261019_101500_000.mkv");
        using (var writer = ActivityLogWriter.Create(AtcSoft.VideoSurveillance.Helpers.RecordingPathHelper.BuildActivityPath(video), Start))
        {
            writer.Add(Start.AddSeconds(1), new MotionDetectorResult(false, MotionHoldChange.None, 9, []));
            writer.Add(Start.AddSeconds(3), new MotionDetectorResult(false, MotionHoldChange.None, 1, []));
        }

        var buckets = await RecordingActivityLoaders.FromFile(video)(4, 4, TestContext.Current.CancellationToken);

        buckets.Should().NotBeNull();
        buckets!.Select(b => b.Level).Should().Equal(0, 9, 0, 1);
        buckets.Select(b => b.IsAnalysed).Should().Equal(false, true, false, true);
    }

    [Fact]
    public async Task A_Recording_Without_An_Index_Has_No_Activity()
    {
        var buckets = await RecordingActivityLoaders.FromFile(System.IO.Path.Combine(folder, "Cam", "Cam_20261019_101500_000.mkv"))(10, 60, TestContext.Current.CancellationToken);

        buckets.Should().BeNull();
    }
}