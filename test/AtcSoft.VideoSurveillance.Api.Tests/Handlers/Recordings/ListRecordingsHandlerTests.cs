namespace AtcSoft.VideoSurveillance.Api.Handlers.Recordings;

public class ListRecordingsHandlerTests
{
    private static ListRecordingsHandler CreateHandler(
        IRecordingService? recordingService = null,
        IApplicationSettingsService? settingsService = null,
        ICameraStorageService? cameraStorageService = null)
    {
        recordingService ??= Substitute.For<IRecordingService>();
        settingsService ??= CreateDefaultSettingsService();
        cameraStorageService ??= Substitute.For<ICameraStorageService>();
        cameraStorageService.GetAllCameras().Returns([]);

        return new ListRecordingsHandler(recordingService, settingsService, cameraStorageService);
    }

    private static IApplicationSettingsService CreateDefaultSettingsService()
    {
        var settingsService = Substitute.For<IApplicationSettingsService>();
        settingsService.Recording.Returns(new RecordingSettings
        {
            RecordingPath = Path.Combine(Path.GetTempPath(), "test-recordings-" + Guid.NewGuid()),
        });
        return settingsService;
    }

    [Fact]
    public async Task ExecuteAsync_NoActiveSessions_ReturnsEmptyList()
    {
        // Arrange
        var recordingService = Substitute.For<IRecordingService>();
        recordingService.GetActiveSessions().Returns([]);
        var handler = CreateHandler(recordingService: recordingService);

        // Act
        var result = await handler.ExecuteAsync(
            new ListRecordingsParameters(CameraId: null),
            CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<Ok<List<Recording>>>().Subject;
        okResult.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WithActiveSessions_ReturnsMappedRecordings()
    {
        // Arrange
        var recordingService = Substitute.For<IRecordingService>();
        var cameraId = Guid.NewGuid();
        var session = new RecordingSession(cameraId, "TestCam", @"C:\recordings\cam1.mp4", isManualRecording: true);
        recordingService.GetActiveSessions().Returns([session]);
        var handler = CreateHandler(recordingService: recordingService);

        // Act
        var result = await handler.ExecuteAsync(
            new ListRecordingsParameters(CameraId: null),
            CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<Ok<List<Recording>>>().Subject;
        okResult.Value.Should().HaveCount(1);
        okResult.Value![0].CameraId.Should().Be(cameraId);
        okResult.Value[0].FilePath.Should().Be(@"C:\recordings\cam1.mp4");
    }

    [Fact]
    public async Task ExecuteAsync_FilterByCameraId_ReturnsOnlyMatchingSessions()
    {
        // Arrange
        var recordingService = Substitute.For<IRecordingService>();
        var targetId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var session1 = new RecordingSession(targetId, "Target", @"C:\rec\cam1.mp4", isManualRecording: true);
        var session2 = new RecordingSession(otherId, "Other", @"C:\rec\cam2.mp4", isManualRecording: true);
        recordingService.GetActiveSessions().Returns([session1, session2]);
        var handler = CreateHandler(recordingService: recordingService);

        // Act
        var result = await handler.ExecuteAsync(
            new ListRecordingsParameters(CameraId: targetId),
            CancellationToken.None);

        // Assert
        var okResult = result.Result.Should().BeOfType<Ok<List<Recording>>>().Subject;
        okResult.Value.Should().HaveCount(1);
        okResult.Value![0].CameraId.Should().Be(targetId);
    }

    [Fact]
    public async Task ExecuteAsync_FindsRecordingsInEveryLayout()
    {
        // Arrange — one flat recording, one in a day folder with its
        // thumbnail in the separate thumbnails folder
        var root = Path.Combine(Path.GetTempPath(), "test-recordings-" + Guid.NewGuid());
        try
        {
            var camera = new CameraConfiguration { Id = Guid.NewGuid() };
            camera.Display.DisplayName = "Front Door";

            var flat = Touch(Path.Combine(root, "Front Door", "Front Door_20261011_101500_000.mkv"));
            Touch(Path.ChangeExtension(flat, ".png"));
            Touch(Path.Combine(root, "Front Door", "2026-10-12", "Front Door_20261012_101500_000.mkv"));
            Touch(Path.Combine(root, "thumbnails", "Front Door", "2026-10-12", "Front Door_20261012_101500_000.png"));
            Touch(Path.Combine(root, "Front Door", "2026-10-13", "Front Door_20261013_101500_000.mkv"));

            var handler = CreateHandlerFor(root, camera);

            // Act
            var result = await handler.ExecuteAsync(
                new ListRecordingsParameters(CameraId: null),
                CancellationToken.None);

            // Assert
            var recordings = result.Result.Should().BeOfType<Ok<List<Recording>>>().Subject.Value!;
            recordings.Should().HaveCount(3);
            recordings.Should().OnlyContain(r => r.CameraId == camera.Id && r.CameraName == "Front Door");
            recordings.Select(r => r.FilePath).Should().BeEquivalentTo(
                "Front Door/Front Door_20261011_101500_000.mkv",
                "Front Door/2026-10-12/Front Door_20261012_101500_000.mkv",
                "Front Door/2026-10-13/Front Door_20261013_101500_000.mkv");
            recordings.Single(r => r.FilePath.Contains("20261011", StringComparison.Ordinal)).HasThumbnail.Should().BeTrue();
            recordings.Single(r => r.FilePath.Contains("20261012", StringComparison.Ordinal)).HasThumbnail.Should().BeTrue();
            recordings.Single(r => r.FilePath.Contains("20261013", StringComparison.Ordinal)).HasThumbnail.Should().BeFalse();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsWhereEachThumbnailIs()
    {
        // Arrange — one thumbnail next to its video, one in the thumbnails folder, one missing
        var root = Path.Combine(Path.GetTempPath(), "test-recordings-" + Guid.NewGuid());
        try
        {
            var camera = new CameraConfiguration { Id = Guid.NewGuid() };
            camera.Display.DisplayName = "Front Door";

            var flat = Touch(Path.Combine(root, "Front Door", "Front Door_20261011_101500_000.mkv"));
            Touch(Path.ChangeExtension(flat, ".png"));
            Touch(Path.Combine(root, "Front Door", "2026-10-12", "Front Door_20261012_101500_000.mkv"));
            Touch(Path.Combine(root, "thumbnails", "Front Door", "2026-10-12", "Front Door_20261012_101500_000.png"));
            Touch(Path.Combine(root, "Front Door", "2026-10-13", "Front Door_20261013_101500_000.mkv"));

            var handler = CreateHandlerFor(root, camera);

            // Act
            var result = await handler.ExecuteAsync(
                new ListRecordingsParameters(CameraId: null),
                CancellationToken.None);

            // Assert
            var recordings = result.Result.Should().BeOfType<Ok<List<Recording>>>().Subject.Value!;
            recordings.Single(r => r.FilePath.Contains("20261011", StringComparison.Ordinal)).ThumbnailPath
                .Should().Be("Front Door/Front Door_20261011_101500_000.png");
            recordings.Single(r => r.FilePath.Contains("20261012", StringComparison.Ordinal)).ThumbnailPath
                .Should().Be("thumbnails/Front Door/2026-10-12/Front Door_20261012_101500_000.png");
            recordings.Single(r => r.FilePath.Contains("20261013", StringComparison.Ordinal)).ThumbnailPath
                .Should().BeNull();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_TakesTheStartTimeFromTheFileName()
    {
        // Arrange — the file's creation time changes when it is moved or copied; its name does not
        var root = Path.Combine(Path.GetTempPath(), "test-recordings-" + Guid.NewGuid());
        try
        {
            var camera = new CameraConfiguration { Id = Guid.NewGuid() };
            camera.Display.DisplayName = "Front Door";
            Touch(Path.Combine(root, "Front Door", "Front Door_20261011_101500_250.mkv"));

            var handler = CreateHandlerFor(root, camera);

            // Act
            var result = await handler.ExecuteAsync(
                new ListRecordingsParameters(CameraId: null),
                CancellationToken.None);

            // Assert — server file names are in UTC
            var recording = result.Result.Should().BeOfType<Ok<List<Recording>>>().Subject.Value!.Single();
            recording.StartedAt.Should().Be(new DateTimeOffset(2026, 10, 11, 10, 15, 0, 250, TimeSpan.Zero));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ARecordingKeepsItsIdBetweenListings()
    {
        // Arrange
        var root = Path.Combine(Path.GetTempPath(), "test-recordings-" + Guid.NewGuid());
        try
        {
            var camera = new CameraConfiguration { Id = Guid.NewGuid() };
            camera.Display.DisplayName = "Front Door";
            Touch(Path.Combine(root, "Front Door", "Front Door_20261011_101500_000.mkv"));
            Touch(Path.Combine(root, "Front Door", "Front Door_20261012_101500_000.mkv"));

            var handler = CreateHandlerFor(root, camera);

            // Act
            var first = (await handler.ExecuteAsync(new ListRecordingsParameters(CameraId: null), CancellationToken.None))
                .Result.Should().BeOfType<Ok<List<Recording>>>().Subject.Value!;
            var second = (await handler.ExecuteAsync(new ListRecordingsParameters(CameraId: null), CancellationToken.None))
                .Result.Should().BeOfType<Ok<List<Recording>>>().Subject.Value!;

            // Assert
            second.Select(r => r.Id).Should().Equal(first.Select(r => r.Id));
            first.Select(r => r.Id).Should().OnlyHaveUniqueItems();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ListsAviRecordings()
    {
        // Arrange — AVI is one of the formats the settings offer
        var root = Path.Combine(Path.GetTempPath(), "test-recordings-" + Guid.NewGuid());
        try
        {
            var camera = new CameraConfiguration { Id = Guid.NewGuid() };
            camera.Display.DisplayName = "Front Door";
            Touch(Path.Combine(root, "Front Door", "Front Door_20261011_101500_000.avi"));

            var handler = CreateHandlerFor(root, camera);

            // Act
            var result = await handler.ExecuteAsync(
                new ListRecordingsParameters(CameraId: null),
                CancellationToken.None);

            // Assert
            result.Result.Should().BeOfType<Ok<List<Recording>>>().Subject.Value!
                .Should().ContainSingle().Which.FilePath.Should().EndWith(".avi");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ARecordingInProgressIsListedByItsRelativePath()
    {
        // Arrange — clients build the playback address from the path, so it
        // has to be relative to the recording root like the finished ones
        var root = Path.Combine(Path.GetTempPath(), "test-recordings-" + Guid.NewGuid());
        try
        {
            var camera = new CameraConfiguration { Id = Guid.NewGuid() };
            camera.Display.DisplayName = "Front Door";
            var running = Touch(Path.Combine(root, "Front Door", "2026-10-13", "Front Door_20261013_101500_000.mkv"));

            var recordingService = Substitute.For<IRecordingService>();
            recordingService.GetActiveSessions().Returns([new RecordingSession(camera.Id, "Front Door", running, isManualRecording: true)]);
            var settingsService = Substitute.For<IApplicationSettingsService>();
            settingsService.Recording.Returns(new RecordingSettings { RecordingPath = root });
            var cameraStorageService = Substitute.For<ICameraStorageService>();
            cameraStorageService.GetAllCameras().Returns([camera]);
            var handler = new ListRecordingsHandler(recordingService, settingsService, cameraStorageService);

            // Act
            var result = await handler.ExecuteAsync(
                new ListRecordingsParameters(CameraId: null),
                CancellationToken.None);

            // Assert
            result.Result.Should().BeOfType<Ok<List<Recording>>>().Subject.Value!
                .Should().ContainSingle().Which.FilePath.Should().Be("Front Door/2026-10-13/Front Door_20261013_101500_000.mkv");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_AFinishedRecordingHasItsLength()
    {
        // Arrange
        var root = Path.Combine(Path.GetTempPath(), "test-recordings-" + Guid.NewGuid());
        try
        {
            var camera = new CameraConfiguration { Id = Guid.NewGuid() };
            camera.Display.DisplayName = "Front Door";
            var video = Touch(Path.Combine(root, "Front Door", "Front Door_20261011_101500_000.mp4"));
            var unreadable = Touch(Path.Combine(root, "Front Door", "Front Door_20261011_111500_000.mp4"));

            var durations = Substitute.For<IRecordingDurationProvider>();
            durations.GetDuration(video).Returns(TimeSpan.FromSeconds(65));
            durations.GetDuration(unreadable).Returns((TimeSpan?)null);

            var handler = CreateHandlerFor(root, durations, camera);

            // Act
            var result = await handler.ExecuteAsync(
                new ListRecordingsParameters(CameraId: null),
                CancellationToken.None);

            // Assert — ISO 8601, as the contract says; empty when the length is not known
            var recordings = result.Result.Should().BeOfType<Ok<List<Recording>>>().Subject.Value!;
            recordings.Single(r => r.FilePath.Contains("101500", StringComparison.Ordinal)).Duration.Should().Be("PT1M5S");
            recordings.Single(r => r.FilePath.Contains("111500", StringComparison.Ordinal)).Duration.Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ARecordingWithAnIndexHasItsSparkline()
    {
        // Arrange
        var root = Path.Combine(Path.GetTempPath(), "test-recordings-" + Guid.NewGuid());
        try
        {
            var camera = new CameraConfiguration { Id = Guid.NewGuid() };
            camera.Display.DisplayName = "Gate";
            var indexed = Touch(Path.Combine(root, "Gate", "Gate_20261012_101500_000.mkv"));
            Touch(Path.Combine(root, "Gate", "Gate_20261012_111500_000.mkv"));
            var start = new DateTimeOffset(2026, 10, 12, 10, 15, 0, TimeSpan.Zero);
            using (var index = ActivityLogWriter.Create(RecordingPathHelper.BuildActivityPath(indexed), start))
            {
                for (var second = 0; second < 240; second++)
                {
                    index.Add(start.AddSeconds(second), new MotionDetectorResult(second == 120, MotionHoldChange.None, second == 120 ? 50 : 0, []));
                }
            }

            var handler = new ListRecordingsHandler(
                Substitute.For<IRecordingService>(),
                SettingsFor(root),
                CamerasOf(camera),
                durationProvider: null,
                sparklines: new ActivitySparklineCache());

            // Act
            var result = await handler.ExecuteAsync(
                new ListRecordingsParameters(CameraId: null),
                CancellationToken.None);

            // Assert — the busy second shows in its stretch; no index, no sparkline
            var recordings = result.Result.Should().BeOfType<Ok<List<Recording>>>().Subject.Value!;
            var sparkline = recordings.Single(r => r.FilePath.Contains("101500", StringComparison.Ordinal)).Activity!;
            sparkline.Should().HaveCount(ActivitySparkline.Points);
            sparkline.Max().Should().Be(50);
            recordings.Single(r => r.FilePath.Contains("111500", StringComparison.Ordinal)).Activity.Should().BeNull();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_TwoCamerasWithTheSameName_DoesNotFail()
    {
        // Arrange
        var root = Path.Combine(Path.GetTempPath(), "test-recordings-" + Guid.NewGuid());
        try
        {
            var first = new CameraConfiguration { Id = Guid.NewGuid() };
            first.Display.DisplayName = "Gate";
            var second = new CameraConfiguration { Id = Guid.NewGuid() };
            second.Display.DisplayName = "Gate";
            Touch(Path.Combine(root, "Gate", "Gate_20261012_101500_000.mkv"));

            var handler = CreateHandlerFor(root, first, second);

            // Act
            var result = await handler.ExecuteAsync(
                new ListRecordingsParameters(CameraId: null),
                CancellationToken.None);

            // Assert
            var recordings = result.Result.Should().BeOfType<Ok<List<Recording>>>().Subject.Value!;
            recordings.Should().ContainSingle().Which.CameraId.Should().Be(first.Id);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static ListRecordingsHandler CreateHandlerFor(
        string recordingPath,
        params CameraConfiguration[] cameras)
        => CreateHandlerFor(recordingPath, durationProvider: null, cameras);

    private static ListRecordingsHandler CreateHandlerFor(
        string recordingPath,
        IRecordingDurationProvider? durationProvider,
        params CameraConfiguration[] cameras)
    {
        var recordingService = Substitute.For<IRecordingService>();
        recordingService.GetActiveSessions().Returns([]);

        var settingsService = Substitute.For<IApplicationSettingsService>();
        settingsService.Recording.Returns(new RecordingSettings { RecordingPath = recordingPath });

        var cameraStorageService = Substitute.For<ICameraStorageService>();
        cameraStorageService.GetAllCameras().Returns([.. cameras]);

        return new ListRecordingsHandler(recordingService, settingsService, cameraStorageService, durationProvider);
    }

    private static IApplicationSettingsService SettingsFor(string recordingPath)
    {
        var settingsService = Substitute.For<IApplicationSettingsService>();
        settingsService.Recording.Returns(new RecordingSettings { RecordingPath = recordingPath });
        return settingsService;
    }

    private static ICameraStorageService CamerasOf(
        params CameraConfiguration[] cameras)
    {
        var cameraStorageService = Substitute.For<ICameraStorageService>();
        cameraStorageService.GetAllCameras().Returns([.. cameras]);
        return cameraStorageService;
    }

    private static string Touch(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, string.Empty);
        return path;
    }
}