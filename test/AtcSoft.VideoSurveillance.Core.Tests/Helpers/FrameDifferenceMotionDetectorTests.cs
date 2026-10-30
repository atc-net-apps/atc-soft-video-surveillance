namespace AtcSoft.VideoSurveillance.Helpers;

/// <summary>
/// Today's detection behind the detector seam: the difference of two pictures, the gate on the
/// share of changed pixels, and the post-motion hold. The services and the storybook scorer use
/// it through <see cref="IMotionDetector"/>, so what is measured is what ships.
/// </summary>
public sealed class FrameDifferenceMotionDetectorTests
{
    private const int Width = 80;
    private const int Height = 60;

    private static readonly DateTimeOffset Start = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void The_First_Picture_Only_Starts_The_Comparison()
    {
        var sut = Create();

        var result = sut.Process(Frame(Plain(0), Start));

        result.Change.Should().Be(MotionHoldChange.None);
        result.IsActive.Should().BeFalse();
        result.Boxes.Should().BeEmpty();
    }

    [Fact]
    public void A_Moving_Square_Starts_Motion_With_A_Box()
    {
        var sut = Create();
        sut.Process(Frame(Plain(0), Start));

        var result = sut.Process(Frame(WithSquare(x: 20, y: 20, size: 20), Start.AddMilliseconds(250)));

        result.Change.Should().Be(MotionHoldChange.Started);
        result.IsActive.Should().BeTrue();
        result.Boxes.Should().NotBeEmpty();
        result.ChangePercent.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Motion_Is_Held_For_The_Post_Motion_Time_Then_Ends()
    {
        var sut = Create(postMotionSeconds: 2);
        sut.Process(Frame(Plain(0), Start));
        var moving = WithSquare(x: 20, y: 20, size: 20);
        sut.Process(Frame(moving, Start.AddSeconds(1)));

        sut.Process(Frame(moving, Start.AddSeconds(2))).IsActive.Should().BeTrue("the hold keeps it on");
        sut.Process(Frame(moving, Start.AddSeconds(3))).Change.Should().Be(MotionHoldChange.Ended);
    }

    [Fact]
    public void A_Picture_Of_Another_Size_Restarts_The_Comparison()
    {
        var sut = Create();
        sut.Process(Frame(Plain(0), Start));

        var result = sut.Process(new MotionFrame(new byte[40 * 30], 40, 30, Start.AddSeconds(1)));

        result.Change.Should().Be(MotionHoldChange.None);
    }

    private static FrameDifferenceMotionDetector Create(
        int postMotionSeconds = 0)
        => new(new MotionDetectionSettings { PostMotionDurationSeconds = postMotionSeconds });

    private static MotionFrame Frame(
        byte[] gray,
        DateTimeOffset time)
        => new(gray, Width, Height, time);

    private static byte[] Plain(byte value)
        => Enumerable.Repeat(value, Width * Height).ToArray();

    private static byte[] WithSquare(
        int x,
        int y,
        int size)
    {
        var frame = Plain(0);
        for (var row = y; row < y + size; row++)
        {
            for (var column = x; column < x + size; column++)
            {
                frame[(row * Width) + column] = 255;
            }
        }

        return frame;
    }
}