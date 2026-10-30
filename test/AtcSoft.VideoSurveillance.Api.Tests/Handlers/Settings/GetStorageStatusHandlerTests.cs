namespace AtcSoft.VideoSurveillance.Api.Handlers.Settings;

public sealed class GetStorageStatusHandlerTests
{
    private readonly IApplicationSettingsService settings = Substitute.For<IApplicationSettingsService>();
    private readonly RecordingSettings recording = new();

    public GetStorageStatusHandlerTests()
        => settings.Recording.Returns(recording);

    [Fact]
    public async Task The_Disk_Of_The_Recording_Folder_Is_Reported_With_What_The_Guard_Keeps_Free()
    {
        recording.RecordingPath = Path.Combine(Path.GetTempPath(), "atcsoft-storage-" + Guid.NewGuid().ToString("N"));
        recording.Cleanup.EnableDiskSpaceGuard = true;
        recording.Cleanup.MinFreeSpaceMb = 100;

        var result = await new GetStorageStatusHandler(settings).ExecuteAsync(TestContext.Current.CancellationToken);

        var status = result.Result.Should().BeOfType<Ok<StorageStatus>>().Subject.Value!;
        status.TotalBytes.Should().BeGreaterThan(0);
        status.FreeBytes.Should().BeInRange(0, status.TotalBytes);
        status.MinFreeBytes.Should().Be(100L * 1024 * 1024);
        status.Text.Should().Contain("free of");
    }

    [Fact]
    public async Task With_The_Guard_Off_The_Disk_Is_Never_Reported_Low()
    {
        recording.RecordingPath = Path.GetTempPath();
        recording.Cleanup.EnableDiskSpaceGuard = false;
        recording.Cleanup.MinFreeSpaceMb = int.MaxValue;

        var result = await new GetStorageStatusHandler(settings).ExecuteAsync(TestContext.Current.CancellationToken);

        result.Result.Should().BeOfType<Ok<StorageStatus>>().Which.Value!.IsLow.Should().BeFalse();
    }

    [Fact]
    public async Task A_Recording_Folder_Without_A_Readable_Disk_Is_Reported_As_Unavailable()
    {
        recording.RecordingPath = "Q:\\does-not-exist\\recordings";

        var result = await new GetStorageStatusHandler(settings).ExecuteAsync(TestContext.Current.CancellationToken);

        ((IStatusCodeHttpResult)result.Result).StatusCode.Should().Be(503);
    }

    [Fact]
    public async Task An_Unreadable_Disk_Says_When_To_Ask_Again()
    {
        recording.RecordingPath = "Q:\\does-not-exist\\recordings";

        var result = await new GetStorageStatusHandler(settings).ExecuteAsync(TestContext.Current.CancellationToken);

        var response = await result.Result.ExecuteAsync();
        response.StatusCode.Should().Be(503);
        response.Headers.RetryAfter.ToString().Should().Be("30");
    }
}