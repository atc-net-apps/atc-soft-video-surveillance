namespace AtcSoft.Video.Surveillance.Blazor.App.Logic;

/// <summary>
/// At most five motion events a second per camera are shown, and the end of
/// motion always is.
/// </summary>
public sealed class MotionThrottleTests
{
    private static readonly Guid Front = Guid.NewGuid();
    private static readonly Guid Back = Guid.NewGuid();

    [Fact]
    public void A_Camera_Shows_At_Most_Five_Motion_Events_A_Second()
    {
        var time = new FakeTimeProvider();
        var sut = new MotionThrottle(time);
        var shown = 0;

        // Twenty-five analysed pictures a second, for a second.
        for (var picture = 0; picture < 25; picture++)
        {
            shown += sut.ShouldShow(Front, isMotionActive: true) ? 1 : 0;
            time.Advance(TimeSpan.FromMilliseconds(40));
        }

        shown.Should().Be(5);
    }

    [Fact]
    public void The_End_Of_Motion_Is_Always_Shown_And_Each_Camera_Counts_Alone()
    {
        var sut = new MotionThrottle(new FakeTimeProvider());

        sut.ShouldShow(Front, isMotionActive: true).Should().BeTrue();
        sut.ShouldShow(Back, isMotionActive: true).Should().BeTrue();
        sut.ShouldShow(Front, isMotionActive: true).Should().BeFalse();
        sut.ShouldShow(Front, isMotionActive: false).Should().BeTrue();
        sut.ShouldShow(Front, isMotionActive: true).Should().BeTrue("motion that starts again is shown at once");
    }
}