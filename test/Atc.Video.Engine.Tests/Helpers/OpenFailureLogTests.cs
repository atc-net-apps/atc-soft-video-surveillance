namespace Atc.Video.Engine.Helpers;

/// <summary>
/// A camera that stays out of reach is retried for days: its first failure is a warning, the
/// same failure again only a debug line, with a warning again once an hour.
/// </summary>
public sealed class OpenFailureLogTests
{
    private static readonly TimeSpan Hour = TimeSpan.FromHours(1);
    private readonly ManualTime time = new();

    [Fact]
    public void The_First_Failure_Is_A_Warning_And_The_Same_Failure_Again_Is_Not()
    {
        var sut = new OpenFailureLog(time, Hour);

        sut.Failed("cam").Level.Should().Be(Microsoft.Extensions.Logging.LogLevel.Warning);

        time.Advance(TimeSpan.FromMinutes(2));
        var again = sut.Failed("cam");

        again.Level.Should().Be(Microsoft.Extensions.Logging.LogLevel.Debug);
        again.Attempts.Should().Be(2);
    }

    [Fact]
    public void A_Camera_Still_Out_Of_Reach_After_An_Hour_Is_Warned_About_Again()
    {
        var sut = new OpenFailureLog(time, Hour);
        sut.Failed("cam");

        OpenFailureNote? last = null;
        for (var i = 0; i < 30; i++)
        {
            time.Advance(TimeSpan.FromMinutes(2));
            last = sut.Failed("cam");
        }

        last!.Level.Should().Be(Microsoft.Extensions.Logging.LogLevel.Warning, "an hour has gone since the last warning");
        last.Attempts.Should().Be(31);
        last.Since.Should().Be(TimeSpan.FromMinutes(60));

        time.Advance(TimeSpan.FromMinutes(2));
        sut.Failed("cam").Level.Should().Be(Microsoft.Extensions.Logging.LogLevel.Debug);
    }

    [Fact]
    public void Each_Camera_Counts_On_Its_Own()
    {
        var sut = new OpenFailureLog(time, Hour);
        sut.Failed("cam");

        sut.Failed("cam").Level.Should().Be(Microsoft.Extensions.Logging.LogLevel.Debug);
        sut.Failed("other").Level.Should().Be(Microsoft.Extensions.Logging.LogLevel.Warning);
    }

    [Fact]
    public void Opening_Tells_How_Many_Attempts_Failed_And_Starts_Counting_Anew()
    {
        var sut = new OpenFailureLog(time, Hour);
        sut.Opened("cam").Should().Be(0, "nothing failed");

        sut.Failed("cam");
        sut.Failed("cam");

        sut.Opened("cam").Should().Be(2);
        sut.Failed("cam").Level.Should().Be(Microsoft.Extensions.Logging.LogLevel.Warning, "a new series of failures");
    }

    private sealed class ManualTime : TimeProvider
    {
        private DateTimeOffset now = new(2026, 11, 18, 12, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow()
            => now;

        public void Advance(TimeSpan by)
            => now += by;
    }
}