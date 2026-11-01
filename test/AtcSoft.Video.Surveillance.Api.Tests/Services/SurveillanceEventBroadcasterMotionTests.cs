namespace AtcSoft.Video.Surveillance.Api.Services;

/// <summary>
/// A motion's start and end are kept for clients that resume; the boxes in between (four a second
/// per camera) only go out live. They filled the replay buffer in minutes.
/// </summary>
public sealed class SurveillanceEventBroadcasterMotionTests
{
    [Fact]
    public async Task Only_A_Motions_Start_And_End_Are_Kept_For_Clients_That_Resume()
    {
        var settings = Substitute.For<IApplicationSettingsService>();
        settings.Recording.Returns(new RecordingSettings());
        settings.MotionDetection.Returns(new MotionDetectionSettings());
        using var recordings = new ServerRecordingService(settings, Substitute.For<ICameraStorageService>(), NullLogger<ServerRecordingService>.Instance);
        var motion = Substitute.For<IMotionDetectionService>();
        var feed = new SurveillanceEventFeed();
        using var sut = new SurveillanceEventBroadcaster(
            NullLogger<SurveillanceEventBroadcaster>.Instance,
            new SurveillanceEventBroadcasterOptions(),
            recordings,
            recordings,
            motion,
            Substitute.For<IUsbCameraLifecycleCoordinator>(),
            feed);
        await sut.StartAsync(TestContext.Current.CancellationToken);
        var before = feed.Publish(new SurveillanceEvent(Type: "connectionStateChanged", CameraId: Guid.NewGuid(), Timestamp: DateTimeOffset.UtcNow));
        var camera = Guid.NewGuid();

        for (var i = 0; i < 10; i++)
        {
            motion.MotionDetected += Raise.EventWith(new MotionDetectedEventArgs(camera, 5, isMotionActive: true));
        }

        motion.MotionDetected += Raise.EventWith(new MotionDetectedEventArgs(camera, 0, isMotionActive: false));

        using var resumed = feed.Subscribe(lastSequence: before);
        resumed.Missed.Should().Be(0);
        resumed.Replayed.Should().HaveCount(2, "the start and the end");
        await sut.StopAsync(TestContext.Current.CancellationToken);
    }
}