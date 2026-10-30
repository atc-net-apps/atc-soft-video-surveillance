namespace AtcSoft.VideoSurveillance.Helpers;

public class ReconnectBackoffTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(-1, 0)]
    [InlineData(1, 30)] // base delay (no doubling on first failure)
    [InlineData(2, 60)]
    [InlineData(3, 120)]
    [InlineData(4, 240)]
    [InlineData(5, 480)]
    [InlineData(6, 900)] // capped at 15 min before raw value (960 → 900)
    [InlineData(10, 900)] // far past cap
    [InlineData(100, 900)] // pathological failure counts still capped
    public void ComputeDelay_Defaults(
        int failures,
        int expectedSeconds)
    {
        // Act
        var delay = ReconnectBackoff.ComputeDelay(failures);

        // Assert
        delay.Should().Be(TimeSpan.FromSeconds(expectedSeconds));
    }

    [Fact]
    public void ComputeDelay_RespectsCustomBaseAndMax()
    {
        // Arrange
        var b = TimeSpan.FromSeconds(2);
        var m = TimeSpan.FromSeconds(10);

        // Act + Assert — 2, 4, 8, 10 (capped)
        ReconnectBackoff.ComputeDelay(1, b, m).Should().Be(TimeSpan.FromSeconds(2));
        ReconnectBackoff.ComputeDelay(2, b, m).Should().Be(TimeSpan.FromSeconds(4));
        ReconnectBackoff.ComputeDelay(3, b, m).Should().Be(TimeSpan.FromSeconds(8));
        ReconnectBackoff.ComputeDelay(4, b, m).Should().Be(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public void ComputeDelay_ZeroFailures_ReturnsZero()
    {
        // Act + Assert
        ReconnectBackoff.ComputeDelay(0).Should().Be(TimeSpan.Zero);
    }
}