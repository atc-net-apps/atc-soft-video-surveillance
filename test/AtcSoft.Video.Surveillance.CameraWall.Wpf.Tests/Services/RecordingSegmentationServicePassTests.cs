namespace AtcSoft.Video.Surveillance.CameraWall.Wpf.Services;

/// <summary>
/// The recording worker looks after the recordings every 30 s: the disk space guard, recordings to
/// start again, segments. One step that fails is logged and the others still run, and each pass is
/// stamped, so a worker that is stuck shows in the diagnostics.
/// </summary>
public sealed class RecordingSegmentationServicePassTests
{
    private readonly IApplicationSettingsService settings = Substitute.For<IApplicationSettingsService>();
    private readonly IRecordingService recording = Substitute.For<IRecordingService>();

    public RecordingSegmentationServicePassTests()
        => settings.Recording.Returns(new RecordingSettings { EnableHourlySegmentation = true, MaxRecordingDurationMinutes = 60 });

    [Fact]
    public async Task A_Failing_Disk_Space_Guard_Does_Not_Keep_Recordings_From_Being_Started_Again()
    {
        recording.EnforceDiskSpaceGuard().Returns(_ => throw new IOException("The drive went away"));
        recording.GetActiveSessions().Returns([]);
        var sut = new RecordingSegmentationService(NullLogger<RecordingSegmentationService>.Instance, settings, recording);

        await sut.DoWorkAsync(TestContext.Current.CancellationToken);

        recording.Received(1).ResumeRecordings();
        recording.Received(1).GetActiveSessions();
    }

    [Fact]
    public async Task Each_Pass_Is_Stamped()
    {
        recording.GetActiveSessions().Returns([]);
        var sut = new RecordingSegmentationService(NullLogger<RecordingSegmentationService>.Instance, settings, recording);
        sut.LastPassUtc.Should().BeNull("no pass yet");

        await sut.DoWorkAsync(TestContext.Current.CancellationToken);

        sut.LastPassUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}