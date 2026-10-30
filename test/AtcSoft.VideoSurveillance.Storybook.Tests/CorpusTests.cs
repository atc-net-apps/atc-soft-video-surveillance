namespace AtcSoft.VideoSurveillance.Storybook;

/// <summary>
/// The corpus of real recordings: every <c>*.review.json</c> in the folder named by
/// <c>ATCSOFT_BBOX_CORPUS</c> (and below it), with its recording beside it, is run as *Analyse
/// recording* reads it and judged by its review. The scores accepted so far are kept in that folder
/// (<c>accepted-scores.json</c>, by the recording's SHA-256), outside git like the recordings and
/// their reviews; a score that gets worse fails. Set <c>ATCSOFT_BBOX_UPDATE_BASELINE=1</c> to accept
/// the current scores. Skipped without the folder.
/// </summary>
public sealed class CorpusTests
{
    private const string CorpusVariable = "ATCSOFT_BBOX_CORPUS";
    private const string UpdateVariable = "ATCSOFT_BBOX_UPDATE_BASELINE";
    private const string AcceptedFile = "accepted-scores.json";
    private const double Tolerance = 0.005;

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    [Fact]
    public void No_Reviewed_Recording_Scores_Worse_Than_Accepted()
    {
        var corpus = Environment.GetEnvironmentVariable(CorpusVariable);
        Assert.SkipWhen(string.IsNullOrWhiteSpace(corpus) || !Directory.Exists(corpus), $"No corpus: set {CorpusVariable} to a folder of recordings with their reviews.");
        EncodedScenes.RequireTool();

        var acceptedPath = Path.Combine(corpus!, AcceptedFile);
        var accepted = File.Exists(acceptedPath)
            ? JsonSerializer.Deserialize<Dictionary<string, AcceptedScore>>(File.ReadAllText(acceptedPath)) ?? []
            : [];
        var update = string.Equals(Environment.GetEnvironmentVariable(UpdateVariable), "1", StringComparison.Ordinal);
        var problems = new List<string>();
        var reviews = Directory.GetFiles(corpus!, "*" + RecordingPathHelper.ReviewExtension, SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToList();
        Assert.SkipWhen(reviews.Count == 0, $"No reviews in {corpus}.");

        foreach (var reviewPath in reviews)
        {
            var review = RecordingReviewFile.Read(reviewPath);
            var video = Path.Combine(Path.GetDirectoryName(reviewPath)!, review.Recording.File);
            if (!File.Exists(video))
            {
                problems.Add($"{Path.GetFileName(reviewPath)}: its recording {review.Recording.File} is not beside it");
                continue;
            }

            var (_, score) = ReviewRunner.Run(video, review, cancellationToken: TestContext.Current.CancellationToken);
            TestContext.Current.TestOutputHelper?.WriteLine($"{review.Recording.File}: {score.Summary}");
            var now = new AcceptedScore(review.Recording.File, Math.Round(score.Recall, 4), Math.Round(score.FalseBoxesPerPicture, 4));
            if (update)
            {
                accepted[review.Recording.Sha256] = now;
                continue;
            }

            if (!accepted.TryGetValue(review.Recording.Sha256, out var before))
            {
                problems.Add($"{review.Recording.File}: no accepted score; run with {UpdateVariable}=1 to accept {score.Summary}");
            }
            else if (now.Recall < before.Recall - Tolerance || now.FalseBoxesPerPicture > before.FalseBoxesPerPicture + Tolerance)
            {
                problems.Add($"{review.Recording.File}: recall {before.Recall:0.000} -> {now.Recall:0.000}, false boxes/picture {before.FalseBoxesPerPicture:0.000} -> {now.FalseBoxesPerPicture:0.000}");
            }
        }

        if (update)
        {
            File.WriteAllText(acceptedPath, JsonSerializer.Serialize(accepted, Options));
        }

        problems.Should().BeEmpty();
    }

    private sealed record AcceptedScore(
        string File,
        double Recall,
        double FalseBoxesPerPicture);
}