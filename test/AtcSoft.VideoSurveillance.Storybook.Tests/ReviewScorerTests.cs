namespace AtcSoft.VideoSurveillance.Storybook;

/// <summary>
/// A reviewed recording is scored by count, only inside its marked stretches: a box for each thing
/// that moves, none where a false box was marked, a moment's allowance at the start of a stretch,
/// and the counts of stretches over the same picture added up.
/// </summary>
public sealed class ReviewScorerTests
{
    [Fact]
    public void Only_The_Marked_Stretches_Are_Judged()
    {
        var review = Review(Span(2, 6, ReviewVerdicts.Correct));
        var frames = Frames(0, 10, t => t is >= 2.5 and < 6 ? 1 : 3);

        var score = ReviewScorer.Score(frames, review);

        score.Recall.Should().Be(1, "one box a picture where one thing moves, past the allowance");
        score.FalseBoxes.Should().Be(0, "the three boxes outside the stretch are not judged");
        score.JudgedPictures.Should().Be(12, "2 to 6 s at 4 a second, less the first second");
    }

    [Fact]
    public void A_Box_Where_A_False_Box_Was_Marked_Counts_Against_The_Detector()
    {
        var review = Review(Span(0, 5, ReviewVerdicts.FalseBox));
        var frames = Frames(0, 5, t => t < 2 ? 1 : 0);

        var score = ReviewScorer.Score(frames, review);

        score.FalseBoxes.Should().Be(8, "two seconds of one box, from the start: a false box has no allowance");
        score.FalseBoxesPerPicture.Should().Be(0.4);
        score.Recall.Should().Be(1, "nothing should have been boxed");
    }

    [Fact]
    public void A_Thing_Not_Boxed_And_A_Box_Too_Many_Are_Both_Counted()
    {
        var review = Review(Span(0, 4, ReviewVerdicts.WrongCount, count: 2));
        var frames = Frames(0, 4, t => t < 2 ? 1 : 3);

        var score = ReviewScorer.Score(frames, review);

        score.Recall.Should().Be(20 / 24.0, "one of two at 1-2 s (past the allowance), both at 2-4 s");
        score.FalseBoxes.Should().Be(8, "one too many a picture at 2-4 s");
    }

    [Fact]
    public void Stretches_Over_The_Same_Picture_Add_Their_Counts()
    {
        var review = Review(Span(0, 10, ReviewVerdicts.Correct), Span(4, 10, ReviewVerdicts.Missed));
        var frames = Frames(0, 10, _ => 2);

        var score = ReviewScorer.Score(frames, review);

        score.FalseBoxes.Should().Be(12, "one box too many while only the first moves (1-4 s), past the second one's allowance none");
        score.Recall.Should().Be(1);
        score.Spans.Select(s => s.Verdict).Should().Equal(ReviewVerdicts.Correct, ReviewVerdicts.Missed);
        score.Spans[1].Pictures.Should().Be(20);
    }

    private static RecordingReview Review(params ReviewSpan[] spans)
    {
        var review = new RecordingReview { Recording = new ReviewRecording { File = "x.mkv", Sha256 = new string('0', 64) } };
        foreach (var span in spans)
        {
            review.Spans.Add(span);
        }

        return review;
    }

    private static ReviewSpan Span(
        double from,
        double to,
        string verdict,
        int? count = null)
        => new() { From = TimeSpan.FromSeconds(from), To = TimeSpan.FromSeconds(to), Verdict = verdict, Count = count };

    // Four pictures a second from 'from' to 'to', each with the number of boxes asked for.
    private static List<ScoredFrame> Frames(
        double from,
        double to,
        Func<double, int> boxes)
        => [.. Enumerable.Range(0, (int)((to - from) * 4)).Select(k => from + (k / 4.0)).Select(t => new ScoredFrame(t, [], IgnoreAll: false, [.. Enumerable.Range(0, boxes(t)).Select(i => new DetectedBox(new PixelBox(i * 10, 0, 5, 5), i))]))];
}