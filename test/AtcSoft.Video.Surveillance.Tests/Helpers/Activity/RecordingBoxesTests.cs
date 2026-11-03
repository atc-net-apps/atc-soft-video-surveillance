namespace AtcSoft.Video.Surveillance.Helpers.Activity;

/// <summary>
/// A recording's motion boxes, found by running it through the detector, and the boxes at a
/// player's position: those of the last picture before it, unless that picture is too far back.
/// </summary>
public sealed class RecordingBoxesTests
{
    private static readonly BoundingBox Person = new() { X = 10, Y = 20, Width = 30, Height = 60, TrackId = 1 };

    [Fact]
    public void A_Position_Shows_The_Boxes_Of_The_Last_Picture_Before_It()
    {
        var sut = Boxes((0.0, []), (1.0, [Person]), (2.0, []));

        sut.At(TimeSpan.FromSeconds(1.2)).Should().Equal(Person);
        sut.At(TimeSpan.FromSeconds(2.1)).Should().BeEmpty();
    }

    [Fact]
    public void Before_The_First_Picture_There_Are_No_Boxes()
        => Boxes((1.0, [Person])).At(TimeSpan.FromSeconds(0.5)).Should().BeEmpty();

    [Fact]
    public void Past_The_Pictures_Analysed_So_Far_There_Are_No_Boxes()
        => Boxes((1.0, [Person])).At(TimeSpan.FromSeconds(3)).Should().BeEmpty();

    [Fact]
    public void Analysing_Keeps_Each_Pictures_Boxes_At_Its_Position()
    {
        var detector = Substitute.For<IMotionDetector>();
        detector.Process(Arg.Any<MotionFrame>()).Returns(
            new MotionDetectorResult(false, MotionHoldChange.None, 0, []),
            new MotionDetectorResult(true, MotionHoldChange.Started, 5, [Person]));
        byte[] picture = new byte[4];

        var sut = RecordingBoxes.Analyse([(TimeSpan.Zero, picture), (TimeSpan.FromSeconds(0.25), picture)], 2, 2, detector, TestContext.Current.CancellationToken);

        sut.Count.Should().Be(2);
        sut.AnalysisWidth.Should().Be(2);
        sut.At(TimeSpan.FromSeconds(0.3)).Should().Equal(Person);
        detector.Received(1).Reset();
    }

    private static RecordingBoxes Boxes(
        params (double Seconds, BoundingBox[] Boxes)[] pictures)
        => new(800, 450, [.. pictures.Select(p => (TimeSpan.FromSeconds(p.Seconds), (IReadOnlyList<BoundingBox>)p.Boxes))]);
}