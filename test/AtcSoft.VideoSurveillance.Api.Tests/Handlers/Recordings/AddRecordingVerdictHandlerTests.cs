namespace AtcSoft.VideoSurveillance.Api.Handlers.Recordings;

using ReviewSpan = AtcSoft.VideoSurveillance.Helpers.Review.ReviewSpan;
using ReviewVerdicts = AtcSoft.VideoSurveillance.Helpers.Review.ReviewVerdicts;

/// <summary>
/// A verdict from a timeline is added to the review beside a finished recording, as the verdict
/// the review knows; a recording still being made, a stretch that is none, or a review that cannot
/// be read is refused.
/// </summary>
public sealed class AddRecordingVerdictHandlerTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "atcsoft-verdict-handler-" + Guid.NewGuid().ToString("N"));
    private readonly IRecordingService recordingService = Substitute.For<IRecordingService>();
    private readonly IRecordingReviewWriter writer = Substitute.For<IRecordingReviewWriter>();
    private readonly AddRecordingVerdictHandler handler;

    public AddRecordingVerdictHandlerTests()
    {
        var settingsService = Substitute.For<IApplicationSettingsService>();
        settingsService.Recording.Returns(new RecordingSettings { RecordingPath = root });
        recordingService.GetActiveSessions().Returns([]);
        handler = new AddRecordingVerdictHandler(settingsService, recordingService, writer);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Theory]
    [InlineData(ReviewVerdict.Correct, ReviewVerdicts.Correct)]
    [InlineData(ReviewVerdict.FalseBox, ReviewVerdicts.FalseBox)]
    [InlineData(ReviewVerdict.Missed, ReviewVerdicts.Missed)]
    [InlineData(ReviewVerdict.WrongCount, ReviewVerdicts.WrongCount)]
    public async Task A_Verdict_Is_Added_To_The_Review_Of_A_Finished_Recording(
        ReviewVerdict verdict,
        string written)
    {
        var video = Video("a");

        var result = await AddAsync("Gate/a.mkv", new AddRecordingVerdictRequest(12.5, 20, verdict, verdict == ReviewVerdict.FalseBox ? null : 2, " person "));

        result.StatusCode.Should().Be(StatusCodes.Status204NoContent);
        writer.Received(1).AddVerdict(video, Arg.Is<ReviewSpan>(s =>
            s.From == TimeSpan.FromSeconds(12.5) && s.To == TimeSpan.FromSeconds(20) && s.Verdict == written &&
            s.Count == (verdict == ReviewVerdict.FalseBox ? null : 2) && s.Label == "person"));
    }

    [Fact]
    public async Task A_Stretch_That_Is_None_Or_A_False_Box_With_A_Count_Is_Refused()
    {
        Video("b");

        (await AddAsync("Gate/b.mkv", new AddRecordingVerdictRequest(20, 20, ReviewVerdict.Correct, null, null))).StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        (await AddAsync("Gate/b.mkv", new AddRecordingVerdictRequest(1, 2, ReviewVerdict.FalseBox, 1, null))).StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        writer.DidNotReceiveWithAnyArgs().AddVerdict(default!, default!);
    }

    [Fact]
    public async Task A_Recording_Still_Being_Made_Is_Refused()
    {
        var video = Video("c");
        recordingService.GetActiveSessions().Returns([new RecordingSession(Guid.NewGuid(), "Gate", video, isManualRecording: true)]);

        (await AddAsync("Gate/c.mkv", Missed())).StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task A_Review_That_Cannot_Be_Read_Is_Not_Written_Over()
    {
        Video("d");
        writer.When(w => w.AddVerdict(Arg.Any<string>(), Arg.Any<ReviewSpan>())).Do(_ => throw new InvalidDataException("version: 7 is not supported"));

        var result = await AddAsync("Gate/d.mkv", Missed());

        result.StatusCode.Should().Be(StatusCodes.Status409Conflict);
        result.Problem!.Detail.Should().Contain("not written over");
    }

    [Fact]
    public async Task A_Missing_Recording_Or_A_Path_Outside_The_Recordings_Is_Not_Reviewed()
    {
        (await AddAsync("Gate/none.mkv", Missed())).StatusCode.Should().Be(StatusCodes.Status404NotFound);
        (await AddAsync("../outside.mkv", Missed())).StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    private static AddRecordingVerdictRequest Missed()
        => new(3, 8, ReviewVerdict.Missed, null, null);

    private Task<global::VideoSurveillance.Generated.Recordings.Results.AddRecordingVerdictResult> AddAsync(
        string filePath,
        AddRecordingVerdictRequest request)
        => handler.ExecuteAsync(new AddRecordingVerdictParameters(filePath, request), TestContext.Current.CancellationToken);

    private string Video(string name)
    {
        var video = Path.Combine(root, "Gate", name + ".mkv");
        Directory.CreateDirectory(Path.GetDirectoryName(video)!);
        File.WriteAllText(video, "video");
        return video;
    }
}