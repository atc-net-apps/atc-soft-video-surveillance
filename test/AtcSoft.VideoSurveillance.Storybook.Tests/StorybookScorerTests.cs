namespace AtcSoft.VideoSurveillance.Storybook;

/// <summary>
/// The scorer, against hand-made box lists: one box per object counts, a second box on the same
/// object is false, a box on a tree is false, and nothing counts during a lighting step.
/// </summary>
public sealed class StorybookScorerTests
{
    private static readonly PixelBox Person = new(100, 100, 40, 120);
    private static readonly PixelBox Tree = new(400, 50, 200, 300);
    private static readonly StorybookExpect Expect = new() { MaxLatencySeconds = 0 };

    [Fact]
    public void A_Box_On_The_Object_In_Every_Frame_Is_A_Perfect_Score()
    {
        var frames = Frames(10, _ => [Box(Person)]);

        var score = StorybookScorer.Score(frames, Expect);

        score.Recall.Should().Be(1);
        score.Precision.Should().Be(1);
        score.FalseBoxes.Should().Be(0);
        score.MeanLatencySeconds.Should().Be(0);
    }

    [Fact]
    public void A_Second_Box_On_The_Same_Object_Is_False()
    {
        var head = new PixelBox(105, 100, 30, 30);
        var frames = Frames(4, _ => [Box(Person), Box(head)]);

        var score = StorybookScorer.Score(frames, Expect);

        score.Recall.Should().Be(1);
        score.FalseBoxes.Should().Be(4);
        score.FalseBoxesPerFrame.Should().Be(1);
    }

    [Fact]
    public void A_Box_On_The_Tree_Is_False_And_Blamed_On_The_Tree()
    {
        var frames = Frames(5, _ => [Box(Person), Box(new PixelBox(450, 100, 50, 50))]);

        var score = StorybookScorer.Score(frames, Expect);

        score.FalseBoxes.Should().Be(5);
        score.FalseBoxesByDistractor["tree"].Should().Be(5);
    }

    [Fact]
    public void A_Late_First_Box_Is_The_Latency_And_Missed_Frames_Lower_The_Recall()
    {
        var frames = Frames(10, i => i < 4 ? [] : [Box(Person)]);

        var score = StorybookScorer.Score(frames, Expect);

        score.MeanLatencySeconds.Should().BeApproximately(1.0, 1e-9, "the first box came at the fifth frame, four frames of 0.25 s in");
        score.Recall.Should().BeApproximately(0.6, 1e-9);
    }

    [Fact]
    public void Frames_Within_The_Latency_Allowance_Do_Not_Count_Against_Recall()
    {
        var frames = Frames(10, i => i < 2 ? [] : [Box(Person)]);

        var score = StorybookScorer.Score(frames, new StorybookExpect { MaxLatencySeconds = 0.5 });

        score.Recall.Should().Be(1);
    }

    [Fact]
    public void Nothing_Counts_While_The_Whole_Picture_Is_Ignored()
    {
        var frames = Frames(4, _ => [Box(new PixelBox(0, 0, 1280, 720))], ignoreAll: true);

        var score = StorybookScorer.Score(frames, Expect);

        score.ScoredFrames.Should().Be(0);
        score.FalseBoxes.Should().Be(0);
    }

    [Fact]
    public void A_Change_Of_Track_Id_On_An_Object_Is_An_Id_Switch()
    {
        var frames = Frames(6, i => [new DetectedBox(Person, TrackId: i < 3 ? 1 : 2)]);

        var score = StorybookScorer.Score(frames, Expect);

        score.IdSwitches.Should().Be(1);
        score.TrackCount.Should().Be(2);
    }

    [Fact]
    public void Without_Track_Ids_The_Track_Measures_Are_Not_Given()
    {
        var score = StorybookScorer.Score(Frames(3, _ => [Box(Person)]), Expect);

        score.TrackCount.Should().BeNull();
        score.IdSwitches.Should().BeNull();
    }

    private static DetectedBox Box(PixelBox box)
        => new(box);

    private static List<ScoredFrame> Frames(
        int count,
        Func<int, IReadOnlyList<DetectedBox>> detected,
        bool ignoreAll = false)
        => Enumerable.Range(0, count)
            .Select(i => new ScoredFrame(
                i * 0.25,
                [
                    new TruthBox("person", Person, TruthState.MustBox, IsDistractor: false),
                    new TruthBox("tree", Tree, TruthState.MustNotBox, IsDistractor: true),
                ],
                ignoreAll,
                detected(i)))
            .ToList();
}