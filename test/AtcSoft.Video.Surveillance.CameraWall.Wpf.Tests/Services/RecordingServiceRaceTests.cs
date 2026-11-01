namespace AtcSoft.Video.Surveillance.CameraWall.Wpf.Services;

/// <summary>
/// Starting, stopping and switching a camera's recording happen one at a time per camera. A stop
/// that came while the segment worker switched the file closed the recorder in the middle; the
/// switch then opened a new file that no session owned: it was never stopped, segmented or protected.
/// </summary>
public sealed class RecordingServiceRaceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "atcsoft-cw-race-" + Guid.NewGuid().ToString("N"));
    private readonly List<CameraConfiguration> cameras = [];

    public RecordingServiceRaceTests()
        => Directory.CreateDirectory(root);

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
        var camera = new CameraConfiguration();
        camera.Display.DisplayName = "Door";
        cameras.Add(camera);

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

        using var sut = CreateSut();
        sut.StartRecording(camera, pipeline).Should().BeTrue();

        var segmenting = Task.Run(() => sut.SegmentRecording(camera.Id), TestContext.Current.CancellationToken);
        inSwitch.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken).Should().BeTrue();
        var stopping = Task.Run(() => sut.StopRecording(camera.Id), TestContext.Current.CancellationToken);
        await Task.Delay(300, TestContext.Current.CancellationToken);
        release.Set();
        await Task.WhenAll(segmenting, stopping);

        sut.IsRecording(camera.Id).Should().BeFalse("the camera was stopped");
        writing.Should().BeNull("no file is written that no session owns");
    }

    private RecordingService CreateSut()
    {
        var settings = Substitute.For<IApplicationSettingsService>();
        settings.Recording.Returns(new RecordingSettings
        {
            RecordingPath = Path.Combine(root, "recordings"),
            Cleanup = new MediaCleanupSettings { EnableDiskSpaceGuard = false },
        });
        settings.CameraDisplay.Returns(new CameraDisplayAppSettings { SnapshotPath = Path.Combine(root, "snapshots") });

        var storage = Substitute.For<ICameraStorageService>();
        storage.GetAllCameras().Returns(_ => cameras.ToList());
        storage.GetCameraById(Arg.Any<Guid>()).Returns(c => cameras.Find(x => x.Id == c.Arg<Guid>()));

        return new RecordingService(
            NullLogger<RecordingService>.Instance,
            settings,
            Substitute.For<IThumbnailGeneratorService>(),
            storage);
    }
}