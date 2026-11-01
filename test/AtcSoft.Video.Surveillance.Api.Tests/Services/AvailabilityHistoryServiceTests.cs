namespace AtcSoft.Video.Surveillance.Api.Services;

/// <summary>
/// The server's history of when cameras could be recorded: its own starts and stops, also when
/// it stopped without saying so.
/// </summary>
public sealed class AvailabilityHistoryServiceTests : IDisposable
{
    private static readonly DateTimeOffset Nine = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);

    private readonly string dataPath = Path.Combine(Path.GetTempPath(), "atcsoft-availability-" + Guid.NewGuid().ToString("N"));
    private readonly Microsoft.Extensions.Time.Testing.FakeTimeProvider time = new(Nine);
    private readonly ServerRecordingService recordingService = new(
        Substitute.For<IApplicationSettingsService>(),
        Substitute.For<ICameraStorageService>(),
        NullLogger<ServerRecordingService>.Instance);

    public void Dispose()
    {
        recordingService.Dispose();
        if (Directory.Exists(dataPath))
        {
            Directory.Delete(dataPath, recursive: true);
        }
    }

    [Fact]
    public async Task A_Server_That_Stopped_Without_Saying_So_Is_Noted_As_Stopped_When_It_Last_Said_It_Was_Alive()
    {
        using (var crashed = Service())
        {
            await crashed.StartAsync(TestContext.Current.CancellationToken);
            time.Advance(TimeSpan.FromMinutes(10));
            await crashed.DoWorkAsync(TestContext.Current.CancellationToken);

            // No StopAsync: the power went.
        }

        time.Advance(TimeSpan.FromMinutes(50));
        using var restarted = Service();
        await restarted.StartAsync(TestContext.Current.CancellationToken);

        restarted.For(Guid.NewGuid()).Should().Equal(
            new AvailabilityEntry(Nine, AvailabilityChange.ServerStarted),
            new AvailabilityEntry(Nine.AddMinutes(10), AvailabilityChange.ServerStopped),
            new AvailabilityEntry(Nine.AddHours(1), AvailabilityChange.ServerStarted));
        await restarted.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_Server_That_Stopped_Properly_Is_Noted_Once()
    {
        using (var first = Service())
        {
            await first.StartAsync(TestContext.Current.CancellationToken);
            time.Advance(TimeSpan.FromMinutes(10));
            await first.StopAsync(TestContext.Current.CancellationToken);
        }

        time.Advance(TimeSpan.FromMinutes(50));
        using var second = Service();
        await second.StartAsync(TestContext.Current.CancellationToken);

        second.For(Guid.NewGuid()).Select(e => e.Change).Should().Equal(
            AvailabilityChange.ServerStarted,
            AvailabilityChange.ServerStopped,
            AvailabilityChange.ServerStarted);
        await second.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task A_Camera_Change_Is_Written_By_The_Queue_And_Read_Back_After_It_Changed()
    {
        var cameraId = Guid.NewGuid();
        using var sut = Service();
        await sut.StartAsync(TestContext.Current.CancellationToken);

        recordingService.Report(cameraId, ConnectionState.Connected);
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < deadline && !sut.For(cameraId).Any(e => e.Change == AvailabilityChange.Online))
        {
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        sut.For(cameraId).Should().Contain(new AvailabilityEntry(Nine, AvailabilityChange.Online));
        await sut.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task History_Older_Than_The_Recordings_Kept_Is_Dropped()
    {
        var cameraId = Guid.NewGuid();
        var file = Path.Combine(dataPath, "availability", cameraId.ToString("N") + ".jsonl");
        AvailabilityLog.Append(file, new AvailabilityEntry(Nine.AddDays(-90), AvailabilityChange.Online));
        AvailabilityLog.Append(file, new AvailabilityEntry(Nine.AddDays(-80), AvailabilityChange.Offline));
        AvailabilityLog.Append(file, new AvailabilityEntry(Nine.AddDays(-2), AvailabilityChange.Online));
        var settings = Substitute.For<IApplicationSettingsService>();
        settings.Recording.Returns(new RecordingSettings { Cleanup = { RecordingRetentionDays = 30 } });
        using var sut = Service(settings);

        await sut.DoWorkAsync(TestContext.Current.CancellationToken);

        AvailabilityLog.Read(file).Select(e => e.Time).Should().Equal(Nine.AddDays(-80), Nine.AddDays(-2));
    }

    // A long startup delay: the tests call DoWorkAsync themselves.
    private AvailabilityHistoryService Service(
        IApplicationSettingsService? settings = null)
        => new(
            NullLogger<AvailabilityHistoryService>.Instance,
            new AvailabilityHistoryServiceOptions { StartupDelaySeconds = ushort.MaxValue },
            recordingService,
            new ServerDataPaths(dataPath),
            time,
            settings);
}