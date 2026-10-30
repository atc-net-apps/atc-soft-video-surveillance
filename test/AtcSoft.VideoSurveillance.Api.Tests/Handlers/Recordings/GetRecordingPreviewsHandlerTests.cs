namespace AtcSoft.VideoSurveillance.Api.Handlers.Recordings;

/// <summary>
/// A client asks where the preview pictures of a recording are, for its timeline. A recording that
/// has none yet is queued for them, and the answer says they are being made.
/// </summary>
public sealed class GetRecordingPreviewsHandlerTests : IDisposable
{
    private const string FilePath = "Garden/Garden_20261019_101500_000.mp4";

    private readonly string root = Path.Combine(Path.GetTempPath(), "atcsoft-recording-previews-" + Guid.NewGuid().ToString("N"));
    private readonly IApplicationSettingsService settingsService = Substitute.For<IApplicationSettingsService>();
    private readonly IRecordingPreviews previews = Substitute.For<IRecordingPreviews>();
    private readonly GetRecordingPreviewsHandler sut;
    private readonly string video;

    public GetRecordingPreviewsHandlerTests()
    {
        settingsService.Recording.Returns(new RecordingSettings { RecordingPath = root });
        video = Path.Combine(root, "Garden", "Garden_20261019_101500_000.mp4");
        Directory.CreateDirectory(Path.GetDirectoryName(video)!);
        File.WriteAllText(video, "x");

        sut = new GetRecordingPreviewsHandler(settingsService, previews);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task The_Previews_Say_Where_Each_Tile_Is_And_Where_The_Picture_Is_Fetched()
    {
        var (layout, _) = RecordingPreviewLayout.Plan(10, 640, 480, [6.5]);
        RecordingPreviewFile.Write(video, layout, path => File.WriteAllBytes(path, [1, 2, 3]));

        var sheet = await OkAsync(FilePath);

        sheet.HasPreviews.Should().BeTrue();
        sheet.ImagePath.Should().Be("Garden/Garden_20261019_101500_000.previews.jpg");
        (sheet.IntervalSeconds, sheet.TileWidth, sheet.TileHeight, sheet.Columns, sheet.GridCount).Should().Be((1d, 160, 120, 20, 11));
        sheet.Events.Should().ContainSingle().Which.Should().Be(new RecordingPreviewTile(Seconds: 6.5, Tile: 11));
        previews.DidNotReceive().TryEnqueue(Arg.Any<string>());
    }

    [Fact]
    public async Task A_Recording_Without_Previews_Is_Queued_For_Them()
    {
        previews.Contains(video).Returns(true);

        var sheet = await OkAsync(FilePath);

        previews.Received(1).TryEnqueue(video);
        sheet.HasPreviews.Should().BeFalse();
        sheet.IsBeingMade.Should().BeTrue();
        sheet.ImagePath.Should().BeNull();
        sheet.Events.Should().BeEmpty();
    }

    [Fact]
    public async Task A_Recording_That_Is_Not_There_Is_Not_Found()
    {
        var result = await sut.ExecuteAsync(new GetRecordingPreviewsParameters(FilePath: "Garden/Garden_20261019_111500_000.mp4"), TestContext.Current.CancellationToken);

        result.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Theory]
    [InlineData("../Garden_20261019_101500_000.mp4")]
    [InlineData("Garden/Garden_20261019_101500_000.previews.jpg")]
    public async Task A_Path_That_Does_Not_Name_A_Recording_Is_Refused(
        string filePath)
    {
        var result = await sut.ExecuteAsync(new GetRecordingPreviewsParameters(FilePath: filePath), TestContext.Current.CancellationToken);

        result.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    private async Task<RecordingPreviewSheet> OkAsync(string filePath)
    {
        var result = await sut.ExecuteAsync(new GetRecordingPreviewsParameters(FilePath: filePath), TestContext.Current.CancellationToken);
        return result.Result.Should().BeOfType<Ok<RecordingPreviewSheet>>().Subject.Value!;
    }
}