namespace AtcSoft.VideoSurveillance.Helpers.Review;

/// <summary>
/// A recording's review (<c>{video}.review.json</c>): what is written is read back as it was,
/// readable by hand, a file that is not a review is refused with what is wrong, and the review
/// moves and is deleted with its recording.
/// </summary>
public sealed class RecordingReviewFileTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "atcsoft-review-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void A_Review_Is_Read_Back_As_It_Was_Written()
    {
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "garden.review.json");
        var review = Review();

        RecordingReviewFile.Write(path, review);
        var read = RecordingReviewFile.Read(path);

        read.Should().BeEquivalentTo(review);
        File.Exists(path + ".part").Should().BeFalse();
    }

    [Fact]
    public void A_Review_Is_Written_To_Be_Read_By_Hand()
    {
        var json = RecordingReviewFile.ToJson(Review());

        json.Should().Contain("\"from\": \"00:47:58.000\"").And.Contain("\"verdict\": \"falseBox\"").And.Contain("\"kind\": \"ignore\"");
        json.Should().NotContain("\"count\": null", "what is not given is left out");
    }

    [Fact]
    public void A_Time_Written_By_Hand_Without_Milliseconds_Is_Read()
    {
        var json = RecordingReviewFile.ToJson(Review()).Replace("00:47:58.000", "00:47:58", StringComparison.Ordinal);

        RecordingReviewFile.Parse(json).Spans[0].From.Should().Be(new TimeSpan(0, 47, 58));
    }

    [Theory]
    [InlineData("\"verdict\": \"falseBox\"", "\"verdict\": \"maybe\"", "spans[1].verdict*maybe*")]
    [InlineData("\"to\": \"00:48:03.000\"", "\"to\": \"00:47:00.000\"", "spans[0]*not a stretch*")]
    [InlineData("\"count\": 2", "\"count\": 0", "spans[0].count*at least 1*")]
    [InlineData("\"from\": \"00:47:58.000\"", "\"from\": \"soon\"", "*soon*not a time*")]
    [InlineData("\"version\": 1", "\"version\": 2", "version*2*")]
    public void A_File_That_Is_No_Review_Is_Refused_With_What_Is_Wrong(
        string from,
        string to,
        string message)
    {
        var json = RecordingReviewFile.ToJson(Review()).Replace(from, to, StringComparison.Ordinal);

        var read = () => RecordingReviewFile.Parse(json, "garden.review.json");

        read.Should().Throw<InvalidDataException>().WithMessage("garden.review.json: " + message);
    }

    [Fact]
    public void A_False_Box_Has_No_Count()
    {
        var review = Review();
        review.Spans[1].Count = 1;

        var write = () => RecordingReviewFile.ToJson(review);

        write.Should().Throw<InvalidDataException>().WithMessage("*spans[1].count*false box*");
    }

    [Fact]
    public void A_Recording_Is_Named_By_The_Sha256_Of_Its_Bytes()
    {
        Directory.CreateDirectory(folder);
        var video = Path.Combine(folder, "garden.mkv");
        File.WriteAllText(video, "abc");

        RecordingReviewFile.Sha256Of(video).Should().Be("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
    }

    [Fact]
    public void A_Review_Belongs_To_Its_Recording()
    {
        var video = Path.Combine(folder, "Garden", "2026-10-17", "Garden_20261017_121619_241.mkv");

        var review = RecordingPathHelper.BuildReviewPath(video);

        review.Should().Be(Path.Combine(folder, "Garden", "2026-10-17", "Garden_20261017_121619_241.review.json"));
        RecordingPathHelper.GetSidecarPaths(video).Should().Contain(review, "it moves with the video when the layout changes");
        RecordingPathHelper.GetCompanionPaths(video, folder).Should().Contain(review, "it is deleted and protected with the video");
    }

    [Fact]
    public void Each_Verdict_Expects_Its_Count()
    {
        ReviewVerdicts.ExpectedCount(new ReviewSpan { Verdict = ReviewVerdicts.Correct }).Should().Be(1);
        ReviewVerdicts.ExpectedCount(new ReviewSpan { Verdict = ReviewVerdicts.WrongCount, Count = 3 }).Should().Be(3);
        ReviewVerdicts.ExpectedCount(new ReviewSpan { Verdict = ReviewVerdicts.Missed, Count = 2 }).Should().Be(2);
        ReviewVerdicts.ExpectedCount(new ReviewSpan { Verdict = ReviewVerdicts.FalseBox }).Should().Be(0);
    }

    private static RecordingReview Review()
        => new()
        {
            Recording = new ReviewRecording { File = "garden.mkv", Sha256 = new string('a', 64) },
            Detector = new ReviewDetector
            {
                Settings = new MotionDetectionSettings { Sensitivity = 70 },
                Zones = [new MotionZone { Name = "Road", Kind = MotionZoneKind.Ignore, Points = [new(0, 0), new(1, 0), new(1, 0.2)] }],
            },
            Spans =
            [
                new ReviewSpan { From = new TimeSpan(0, 47, 58), To = new TimeSpan(0, 48, 3), Verdict = ReviewVerdicts.Correct, Count = 2, Label = "person" },
                new ReviewSpan { From = new TimeSpan(0, 12, 0), To = new TimeSpan(0, 14, 30), Verdict = ReviewVerdicts.FalseBox, Note = "tree" },
                new ReviewSpan
                {
                    From = new TimeSpan(0, 31, 10),
                    To = new TimeSpan(0, 31, 14),
                    Verdict = ReviewVerdicts.Missed,
                    Label = "cat",
                    Box = new ReviewBox { At = new TimeSpan(0, 0, 31, 12, 250), X = 0.71, Y = 0.8, W = 0.04, H = 0.03 },
                },
            ],
        };
}