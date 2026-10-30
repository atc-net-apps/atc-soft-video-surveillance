namespace AtcSoft.VideoSurveillance.Api.Handlers.Recordings;

public sealed class DeleteRecordingHandlerTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "atcsoft-delete-recording-" + Guid.NewGuid().ToString("N"));
    private readonly IRecordingService recordingService = Substitute.For<IRecordingService>();
    private readonly IApplicationSettingsService settingsService = Substitute.For<IApplicationSettingsService>();
    private readonly DeleteRecordingHandler sut;
    private readonly string video;

    public DeleteRecordingHandlerTests()
    {
        settingsService.Recording.Returns(new RecordingSettings { RecordingPath = root });
        recordingService.GetActiveSessions().Returns([]);
        video = Path.Combine(root, "Garden", "Garden_20261012_101500_000.mp4");
        Directory.CreateDirectory(Path.GetDirectoryName(video)!);
        File.WriteAllText(video, "x");

        sut = new DeleteRecordingHandler(recordingService, settingsService);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task A_Finished_Recording_Is_Deleted()
    {
        var result = await ExecuteAsync("Garden/Garden_20261012_101500_000.mp4");

        result.Result.Should().BeOfType<NoContent>();
        File.Exists(video).Should().BeFalse();
    }

    [Fact]
    public async Task A_Recording_The_Server_Is_Still_Making_Is_Not_Deleted()
    {
        var cameraId = Guid.NewGuid();
        recordingService.GetActiveSessions().Returns([new RecordingSession(cameraId, "Garden", video, isManualRecording: true)]);

        var result = await ExecuteAsync("Garden/Garden_20261012_101500_000.mp4");

        result.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        File.Exists(video).Should().BeTrue();
    }

    [Fact]
    public async Task A_Recording_That_Is_Not_There_Is_Not_Found()
    {
        var result = await ExecuteAsync("Garden/Garden_20261012_111500_000.mp4");

        result.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Theory]
    [InlineData("../Garden_20261012_101500_000.mp4")]
    [InlineData("Garden/notes.txt")]
    public async Task A_Path_That_Does_Not_Name_A_Recording_Is_Refused(
        string filePath)
    {
        var result = await ExecuteAsync(filePath);

        result.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        File.Exists(video).Should().BeTrue();
    }

    private Task<global::VideoSurveillance.Generated.Recordings.Results.DeleteRecordingResult> ExecuteAsync(
        string filePath)
        => sut.ExecuteAsync(
            new DeleteRecordingParameters(FilePath: filePath),
            TestContext.Current.CancellationToken);
}