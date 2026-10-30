namespace AtcSoft.VideoSurveillance.Helpers;

/// <summary>
/// Motion stays on for the post-motion time after the last moving picture, on the desktop as on
/// the server. Ending it on the first quiet picture made the boxes blink.
/// </summary>
public sealed class MotionHoldTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Motion_Starts_On_The_First_Moving_Picture()
    {
        var sut = new MotionHold(TimeSpan.FromSeconds(3));

        sut.Update(moving: true, Start).Should().Be(MotionHoldChange.Started);
        sut.IsActive.Should().BeTrue();
    }

    [Fact]
    public void A_Quiet_Picture_Within_The_Hold_Keeps_Motion_On()
    {
        var sut = new MotionHold(TimeSpan.FromSeconds(3));
        sut.Update(moving: true, Start);

        sut.Update(moving: false, Start.AddSeconds(1)).Should().Be(MotionHoldChange.None);
        sut.IsActive.Should().BeTrue();
        sut.Update(moving: true, Start.AddSeconds(2)).Should().Be(MotionHoldChange.Continues);
    }

    [Fact]
    public void Motion_Ends_When_The_Hold_Has_Passed_Since_The_Last_Moving_Picture()
    {
        var sut = new MotionHold(TimeSpan.FromSeconds(3));
        sut.Update(moving: true, Start);
        sut.Update(moving: true, Start.AddSeconds(2));

        sut.Update(moving: false, Start.AddSeconds(4)).Should().Be(MotionHoldChange.None, "3 s have not passed since the last movement at 2 s");
        sut.Update(moving: false, Start.AddSeconds(5)).Should().Be(MotionHoldChange.Ended);
        sut.IsActive.Should().BeFalse();
    }

    [Fact]
    public void Without_A_Hold_Motion_Ends_On_The_First_Quiet_Picture()
    {
        var sut = new MotionHold(TimeSpan.Zero);
        sut.Update(moving: true, Start);

        sut.Update(moving: false, Start.AddMilliseconds(100)).Should().Be(MotionHoldChange.Ended);
    }

    [Fact]
    public void Quiet_Pictures_Without_Motion_Change_Nothing()
        => new MotionHold(TimeSpan.FromSeconds(3)).Update(moving: false, Start).Should().Be(MotionHoldChange.None);
}