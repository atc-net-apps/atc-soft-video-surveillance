namespace AtcSoft.VideoSurveillance.Helpers.Review;

/// <summary>
/// A right-click on a timeline: on an event's band, correct or false alarm over the whole event;
/// anywhere, missed over a few seconds around the place, inside the recording; and verdicts given
/// that way are added to the review beside the recording.
/// </summary>
public sealed class TimelineVerdictsTests : IDisposable
{
    private static readonly ActivityEvent[] Events = [new(0.2, 0.3), new(0.6, 0.65)];

    private readonly string folder = Path.Combine(Path.GetTempPath(), "atcsoft-verdicts-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void On_An_Event_It_Is_Judged_Whole_And_Missed_Is_Offered_Too()
    {
        var verdicts = TimelineVerdicts.At(Events, 0.25, durationSeconds: 100);

        verdicts.Should().Equal(
            new TimelineVerdict(ReviewVerdicts.Correct, 20, 30),
            new TimelineVerdict(ReviewVerdicts.FalseBox, 20, 30),
            new TimelineVerdict(ReviewVerdicts.Missed, 22.5, 27.5));
    }

    [Fact]
    public void Beside_The_Events_Only_Missed_Is_Offered()
    {
        TimelineVerdicts.At(Events, 0.45, durationSeconds: 100).Should().Equal(new TimelineVerdict(ReviewVerdicts.Missed, 42.5, 47.5));
    }

    [Fact]
    public void A_Narrow_Band_Is_Hit_Beside_It_As_It_Is_Drawn_Wider()
    {
        TimelineVerdicts.At(Events, 0.66, durationSeconds: 100, slack: 0.02).Select(v => v.Verdict).Should().Contain(ReviewVerdicts.Correct);
        TimelineVerdicts.At(Events, 0.66, durationSeconds: 100).Select(v => v.Verdict).Should().Equal(ReviewVerdicts.Missed);
    }

    [Theory]
    [InlineData(0, 0, 5)]
    [InlineData(1, 95, 100)]
    public void Missed_At_An_End_Stays_Inside_The_Recording(
        double place,
        double from,
        double to)
    {
        TimelineVerdicts.At([], place, durationSeconds: 100).Should().Equal(new TimelineVerdict(ReviewVerdicts.Missed, from, to));
    }

    [Fact]
    public void A_Recording_Shorter_Than_The_Stretch_Is_Missed_Whole()
    {
        TimelineVerdicts.At([], 0.5, durationSeconds: 3).Should().Equal(new TimelineVerdict(ReviewVerdicts.Missed, 0, 3));
        TimelineVerdicts.At([], 0.5, durationSeconds: 0).Should().BeEmpty();
    }

    [Fact]
    public void Verdicts_Are_Added_To_The_Review_Beside_The_Recording()
    {
        var video = Video("abc");
        var settings = new MotionDetectionSettings { Sensitivity = 80 };
        MotionZone[] zones = [new MotionZone { Name = "Road", Points = [new(0, 0), new(1, 0), new(1, 1)] }];

        RecordingReviews.AddVerdict(video, new TimelineVerdict(ReviewVerdicts.Correct, 20, 30).ToSpan(), settings, zones);
        var review = RecordingReviews.AddVerdict(video, new TimelineVerdict(ReviewVerdicts.FalseBox, 60, 65).ToSpan(), new MotionDetectionSettings(), []);

        RecordingReviewFile.Read(RecordingPathHelper.BuildReviewPath(video)).Should().BeEquivalentTo(review);
        review.Spans.Select(s => s.Verdict).Should().Equal(ReviewVerdicts.Correct, ReviewVerdicts.FalseBox);
        review.Recording.Sha256.Should().Be(RecordingReviewFile.Sha256Of(video));
        review.Detector.Settings.Sensitivity.Should().Be(80, "a review keeps the settings it was begun with");
        review.Detector.Zones.Should().ContainSingle().Which.Name.Should().Be("Road");
    }

    [Fact]
    public void A_Review_Of_The_File_Before_It_Changed_Is_Begun_Again()
    {
        var video = Video("abc");
        RecordingReviews.AddVerdict(video, new TimelineVerdict(ReviewVerdicts.Correct, 1, 2).ToSpan(), new MotionDetectionSettings(), []);
        File.WriteAllText(video, "abcd");

        var review = RecordingReviews.AddVerdict(video, new TimelineVerdict(ReviewVerdicts.Missed, 3, 8).ToSpan(), new MotionDetectionSettings(), []);

        review.Spans.Should().ContainSingle().Which.Verdict.Should().Be(ReviewVerdicts.Missed);
    }

    [Fact]
    public void A_Review_That_Cannot_Be_Read_Is_Not_Written_Over()
    {
        var video = Video("abc");
        var path = RecordingPathHelper.BuildReviewPath(video);
        File.WriteAllText(path, "{ \"version\": 7 }");

        var add = () => RecordingReviews.AddVerdict(video, new TimelineVerdict(ReviewVerdicts.Missed, 3, 8).ToSpan(), new MotionDetectionSettings(), []);

        add.Should().Throw<InvalidDataException>();
        File.ReadAllText(path).Should().Be("{ \"version\": 7 }");
    }

    private string Video(string content)
    {
        Directory.CreateDirectory(folder);
        var video = Path.Combine(folder, "Garden_20261017_121619_241.mkv");
        File.WriteAllText(video, content);
        return video;
    }
}