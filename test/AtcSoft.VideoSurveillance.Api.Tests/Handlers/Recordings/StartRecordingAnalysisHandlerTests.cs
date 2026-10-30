namespace AtcSoft.VideoSurveillance.Api.Handlers.Recordings;

/// <summary>
/// *Analyse recording* on the server: a finished recording without an index is queued once; one
/// with an index, one still being made, or one queued already is refused.
/// </summary>
public sealed class StartRecordingAnalysisHandlerTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "atcsoft-analyse-handler-" + Guid.NewGuid().ToString("N"));
    private readonly IRecordingService recordingService = Substitute.For<IRecordingService>();
    private readonly IRecordingAnalyses queue = Substitute.For<IRecordingAnalyses>();
    private readonly StartRecordingAnalysisHandler handler;

    public StartRecordingAnalysisHandlerTests()
    {
        var settingsService = Substitute.For<IApplicationSettingsService>();
        settingsService.Recording.Returns(new RecordingSettings { RecordingPath = root });
        recordingService.GetActiveSessions().Returns([]);
        queue.TryEnqueue(Arg.Any<string>()).Returns(true);
        handler = new StartRecordingAnalysisHandler(settingsService, recordingService, queue);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task A_Finished_Recording_Without_An_Index_Is_Queued()
    {
        var video = Video("a");

        var result = await StartAsync("Gate/a.mkv");

        result.Result.Should().BeAssignableTo<Microsoft.AspNetCore.Http.IStatusCodeHttpResult>()
            .Which.StatusCode.Should().Be(StatusCodes.Status202Accepted);
        queue.Received(1).TryEnqueue(video);
    }

    [Fact]
    public async Task A_Recording_With_An_Index_Is_Refused()
    {
        var video = Video("b");
        await File.WriteAllTextAsync(RecordingPathHelper.BuildActivityPath(video), "{}", TestContext.Current.CancellationToken);

        (await StartAsync("Gate/b.mkv")).StatusCode.Should().Be(StatusCodes.Status409Conflict);
        queue.DidNotReceive().TryEnqueue(Arg.Any<string>());
    }

    [Fact]
    public async Task A_Recording_Still_Being_Made_Is_Refused()
    {
        var video = Video("c");
        recordingService.GetActiveSessions().Returns([new RecordingSession(Guid.NewGuid(), "Gate", video, isManualRecording: true)]);

        (await StartAsync("Gate/c.mkv")).StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task A_Recording_Queued_Already_Is_Refused()
    {
        Video("d");
        queue.TryEnqueue(Arg.Any<string>()).Returns(false);

        (await StartAsync("Gate/d.mkv")).StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task A_Missing_Recording_Or_A_Path_Outside_The_Recordings_Is_Not_Analysed()
    {
        (await StartAsync("Gate/none.mkv")).StatusCode.Should().Be(StatusCodes.Status404NotFound);
        (await StartAsync("../outside.mkv")).StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    private Task<global::VideoSurveillance.Generated.Recordings.Results.StartRecordingAnalysisResult> StartAsync(
        string filePath)
        => handler.ExecuteAsync(new StartRecordingAnalysisParameters(FilePath: filePath), TestContext.Current.CancellationToken);

    private string Video(string name)
    {
        var video = Path.Combine(root, "Gate", name + ".mkv");
        Directory.CreateDirectory(Path.GetDirectoryName(video)!);
        File.WriteAllText(video, "video");
        return video;
    }
}