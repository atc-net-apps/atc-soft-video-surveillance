namespace AtcSoft.Video.Surveillance.Api.Services;

/// <summary>
/// Starting, stopping and switching a camera's recording happen one at a time per camera. A motion
/// that ended while the segment worker switched the file closed the recorder in the middle; the
/// switch then opened a new file that no session owned, and nothing ended it.
/// </summary>
public sealed class ServerRecordingServiceRaceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "atcsoft-server-race-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task A_Stop_During_A_Segment_Switch_Leaves_No_File_Being_Written_That_Nobody_Owns()
    {
        var recording = new RecordingSettings { RecordingPath = root, RecordingFormat = "mkv" };
        recording.Cleanup.EnableDiskSpaceGuard = false;
        var settings = Substitute.For<IApplicationSettingsService>();
        settings.Recording.Returns(recording);
        settings.MotionDetection.Returns(new MotionDetectionSettings { CooldownSeconds = 0 });
        var camera = new CameraConfiguration { Id = Guid.NewGuid() };
        camera.Display.DisplayName = "Front Door";
        var storage = Substitute.For<ICameraStorageService>();
        storage.GetCameraById(camera.Id).Returns(camera);

        string? writing = null;
        using var inSwitch = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var pipeline = Substitute.For<IMediaPipeline>();
        pipeline.When(p => p.StartRecording(Arg.Any<string>())).Do(c => writing = c.Arg<string>());
        pipeline.When(p => p.StopRecording()).Do(_ => writing = null);
        pipeline.When(p => p.SwitchRecording(Arg.Any<string>())).Do(c =>
        {
            inSwitch.Set();
            release.Wait(TimeSpan.FromSeconds(5));

            // The engine opens a new file whether or not one was open.
            writing = c.Arg<string>();
        });

        using var sut = new ServerRecordingService(settings, storage, NullLogger<ServerRecordingService>.Instance);
        sut.TriggerMotionRecording(camera, pipeline).Should().BeTrue();

        var segmenting = Task.Run(() => sut.SegmentRecording(camera.Id), TestContext.Current.CancellationToken);
        inSwitch.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken).Should().BeTrue();
        var stopping = Task.Run(() => sut.StopRecording(camera.Id), TestContext.Current.CancellationToken);
        await Task.Delay(300, TestContext.Current.CancellationToken);
        release.Set();
        await Task.WhenAll(segmenting, stopping);

        sut.IsRecording(camera.Id).Should().BeFalse("the motion ended");
        writing.Should().BeNull("no file is written that no session owns");
    }
}