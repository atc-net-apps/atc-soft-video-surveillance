namespace AtcSoft.VideoSurveillance.Storybook;

/// <summary>
/// A recording with a review is run as *Analyse recording* reads it and judged by its review:
/// here S3's walker, encoded by the FFmpeg tool, reviewed as a person would mark it. Needs the
/// FFmpeg libraries and its command-line tool; skipped without them.
/// </summary>
public sealed class ReviewedRecordingTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "atcsoft-reviewed-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            Directory.Delete(folder, recursive: true);
        }
        catch (IOException)
        {
            // Best effort: a temp folder left behind is harmless.
        }
    }

    [Fact]
    public void A_Reviewed_Recording_Is_Judged_By_Its_Stretches()
    {
        var video = Walker();
        var review = Review(video);

        var (run, score) = ReviewRunner.Run(video, review, cancellationToken: TestContext.Current.CancellationToken);
        TestContext.Current.TestOutputHelper?.WriteLine(score.Summary);

        run.Frames.Should().NotBeEmpty();
        run.Frames.Where(f => f.Time < RecordingAnalysis.WarmUpSeconds).Should().OnlyContain(f => f.Detected.Count == 0, "the first seconds only teach the scene, as in the apps");
        score.Recall.Should().BeGreaterThanOrEqualTo(0.95, "the walker is boxed where it was marked");
        score.FalseBoxes.Should().Be(0);
        score.Spans.Should().ContainSingle().Which.Pictures.Should().BeGreaterThan(20);
    }

    [Fact]
    public void A_Review_Of_Another_File_Is_Not_Used()
    {
        var video = Walker();
        var review = Review(video);
        review.Recording.Sha256 = new string('0', 64);

        var run = () => ReviewRunner.Run(video, review, cancellationToken: TestContext.Current.CancellationToken);

        run.Should().Throw<InvalidDataException>().WithMessage("*another file*");
    }

    // S3, the slow walker (1 to 11 s), as a camera would send it.
    private string Walker()
    {
        var tool = EncodedScenes.RequireTool();
        var scene = StorybookReader.ReadFile(Path.Combine(AppContext.BaseDirectory, "Scenes", "s03-slow-walker.storybook.json"));
        scene.Video.BitrateKbps = 2000;
        Directory.CreateDirectory(folder);
        var video = Path.Combine(folder, "walker.mkv");
        StorybookEncoder.Encode(scene, tool, video, TestContext.Current.CancellationToken);
        return video;
    }

    // As a person marks it: one person walking, from when the warm-up is over until shortly before they stop.
    private static RecordingReview Review(string video)
        => new()
        {
            Recording = new ReviewRecording { File = Path.GetFileName(video), Sha256 = RecordingReviewFile.Sha256Of(video) },
            Spans = { new ReviewSpan { From = TimeSpan.FromSeconds(RecordingAnalysis.WarmUpSeconds), To = TimeSpan.FromSeconds(10.5), Verdict = ReviewVerdicts.Correct, Count = 1, Label = "person" } },
        };
}