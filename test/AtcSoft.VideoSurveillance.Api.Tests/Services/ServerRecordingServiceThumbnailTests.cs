namespace AtcSoft.VideoSurveillance.Api.Services;

/// <summary>
/// The server makes a thumbnail for every recording segment, the way the
/// standalone app does, and puts it where the folder layout says.
/// </summary>
public sealed class ServerRecordingServiceThumbnailTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "atcsoft-server-rec-" + Guid.NewGuid().ToString("N"));
    private readonly RecordingSettings recordingSettings;
    private readonly IApplicationSettingsService settingsService = Substitute.For<IApplicationSettingsService>();
    private readonly ICameraStorageService cameraStorageService = Substitute.For<ICameraStorageService>();
    private readonly IThumbnailGeneratorService thumbnailService = Substitute.For<IThumbnailGeneratorService>();
    private readonly IMediaPipeline pipeline = Substitute.For<IMediaPipeline>();
    private readonly CameraConfiguration camera = new() { Id = Guid.NewGuid() };

    public ServerRecordingServiceThumbnailTests()
    {
        recordingSettings = new RecordingSettings { RecordingPath = root, RecordingFormat = "mkv" };
        recordingSettings.Cleanup.EnableDiskSpaceGuard = false;
        settingsService.Recording.Returns(recordingSettings);

        camera.Display.DisplayName = "Front Door";
        cameraStorageService.GetCameraById(camera.Id).Returns(camera);
    }

    public void Dispose()
    {
        pipeline.Dispose();
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Starting_A_Recording_Starts_Its_Thumbnail_In_The_Thumbnails_Folder()
    {
        using var sut = CreateSut();

        sut.StartRecording(camera, pipeline).Should().BeTrue();

        var videoPath = sut.GetSession(camera.Id)!.CurrentFilePath;
        thumbnailService.Received(1).StartCapture(
            camera.Id,
            "Front Door",
            pipeline,
            videoPath,
            4,
            RecordingPathHelper.BuildThumbnailPath(videoPath, root, new RecordingLayoutOptions(UseDailySubfolders: true, SeparateThumbnailFolder: true)));
    }

    // The server keeps every recording below its own folder, the one it lists, serves (behind
    // the media token), cleans and reorganises. A camera imported from CameraWall with a folder
    // of its own recorded where the server could not see it.
    [Fact]
    public void A_Cameras_Own_Recording_Folder_Is_Not_Used_By_The_Server()
    {
        camera.Overrides.Recording.RecordingPath = Path.Combine(Path.GetTempPath(), "atcsoft-elsewhere-" + Guid.NewGuid().ToString("N"));
        using var sut = CreateSut();

        sut.StartRecording(camera, pipeline).Should().BeTrue();

        var videoPath = sut.GetSession(camera.Id)!.CurrentFilePath;
        videoPath.Should().StartWith(root + Path.DirectorySeparatorChar);
        Directory.Exists(camera.Overrides.Recording.RecordingPath).Should().BeFalse();
    }

    [Fact]
    public void The_Thumbnail_Follows_The_Layout_And_The_Tile_Count()
    {
        recordingSettings.UseDailySubfolders = false;
        recordingSettings.SeparateThumbnailFolder = false;
        recordingSettings.ThumbnailTileCount = 1;
        using var sut = CreateSut();

        sut.StartRecording(camera, pipeline);

        var videoPath = sut.GetSession(camera.Id)!.CurrentFilePath;
        thumbnailService.Received(1).StartCapture(
            camera.Id,
            "Front Door",
            pipeline,
            videoPath,
            1,
            Path.ChangeExtension(videoPath, ".png"));
    }

    [Fact]
    public void Stopping_A_Recording_Finishes_The_Thumbnail_Before_The_Pipeline_Is_Disposed()
    {
        using var sut = CreateSut();
        sut.StartRecording(camera, pipeline);

        sut.StopRecording(camera.Id);

        Received.InOrder(() =>
        {
            thumbnailService.StopCapture(camera.Id);
            pipeline.Dispose();
        });
    }

    [Fact]
    public void A_New_Segment_Finishes_The_Old_Thumbnail_And_Starts_One_For_The_New_File()
    {
        using var sut = CreateSut();
        sut.StartRecording(camera, pipeline);
        var firstVideo = sut.GetSession(camera.Id)!.CurrentFilePath;
        thumbnailService.ClearReceivedCalls();

        sut.SegmentRecording(camera.Id).Should().BeTrue();

        var secondVideo = sut.GetSession(camera.Id)!.CurrentFilePath;
        secondVideo.Should().NotBe(firstVideo);
        Received.InOrder(() =>
        {
            thumbnailService.StopCapture(camera.Id);
            pipeline.SwitchRecording(secondVideo);
            thumbnailService.StartCapture(
                camera.Id,
                "Front Door",
                pipeline,
                secondVideo,
                4,
                RecordingPathHelper.BuildThumbnailPath(secondVideo, root, new RecordingLayoutOptions(UseDailySubfolders: true, SeparateThumbnailFolder: true)));
        });
    }

    [Fact]
    public void Recording_Works_Without_A_Thumbnail_Generator()
    {
        using var sut = new ServerRecordingService(settingsService, cameraStorageService, NullLogger<ServerRecordingService>.Instance);

        sut.StartRecording(camera, pipeline).Should().BeTrue();
        sut.SegmentRecording(camera.Id).Should().BeTrue();
        sut.StopRecording(camera.Id);

        sut.IsRecording(camera.Id).Should().BeFalse();
    }

    [Fact]
    public void A_Failing_Thumbnail_Generator_Does_Not_Stop_The_Recording()
    {
        thumbnailService
            .When(t => t.StartCapture(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<IMediaPipeline>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>()))
            .Do(_ => throw new InvalidOperationException("no GDI+"));
        using var sut = CreateSut();

        sut.StartRecording(camera, pipeline).Should().BeTrue();

        sut.IsRecording(camera.Id).Should().BeTrue();
    }

    private ServerRecordingService CreateSut()
        => new(settingsService, cameraStorageService, NullLogger<ServerRecordingService>.Instance, thumbnailService);
}