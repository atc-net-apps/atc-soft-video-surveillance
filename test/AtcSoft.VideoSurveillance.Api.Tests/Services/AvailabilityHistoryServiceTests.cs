namespace AtcSoft.VideoSurveillance.Api.Services;

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

    // A long startup delay: the tests call DoWorkAsync themselves.
    private AvailabilityHistoryService Service()
        => new(
            NullLogger<AvailabilityHistoryService>.Instance,
            new AvailabilityHistoryServiceOptions { StartupDelaySeconds = ushort.MaxValue },
            recordingService,
            new ServerDataPaths(dataPath),
            time);
}