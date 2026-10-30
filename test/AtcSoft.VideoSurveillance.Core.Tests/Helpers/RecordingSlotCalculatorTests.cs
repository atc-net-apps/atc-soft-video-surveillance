namespace AtcSoft.VideoSurveillance.Helpers;

public class RecordingSlotCalculatorTests
{
    [Theory]
    [InlineData(0, 0, 15, 0)] // 00:00 with 15-min interval → slot 0
    [InlineData(0, 14, 15, 0)] // 00:14 → still slot 0
    [InlineData(0, 15, 15, 1)] // 00:15 → slot 1
    [InlineData(12, 30, 15, 50)] // 12:30 → slot 50
    [InlineData(23, 59, 15, 95)] // 23:59 → slot 95 (final slot of day)
    [InlineData(0, 0, 60, 0)] // 00:00 with hourly interval → slot 0
    [InlineData(23, 59, 60, 23)] // 23:59 with hourly → slot 23
    public void ComputeSlot_ProducesExpectedSlot(
        int hour,
        int minute,
        int intervalMinutes,
        int expectedSlot)
    {
        // Arrange
        var when = new DateTime(2026, 4, 28, hour, minute, 0, DateTimeKind.Local);

        // Act
        var (_, slot) = RecordingSlotCalculator.ComputeSlot(when, intervalMinutes);

        // Assert
        slot.Should().Be(expectedSlot);
    }

    [Fact]
    public void ComputeSlot_IncludesTheCalendarDate()
    {
        // Arrange
        var when = new DateTime(2026, 4, 28, 12, 30, 0, DateTimeKind.Local);

        // Act
        var (date, _) = RecordingSlotCalculator.ComputeSlot(when, 15);

        // Assert
        date.Should().Be(new DateOnly(2026, 4, 28));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-15)]
    public void ComputeSlot_NonPositiveInterval_Throws(int intervalMinutes)
    {
        // Act
        var act = () => RecordingSlotCalculator.ComputeSlot(DateTime.Now, intervalMinutes);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void IsNewBoundary_NextSlotSameDay_ReturnsTrue()
    {
        // Arrange
        var last = (new DateOnly(2026, 4, 28), 5);
        var current = (new DateOnly(2026, 4, 28), 6);

        // Act + Assert
        RecordingSlotCalculator.IsNewBoundary(current, last).Should().BeTrue();
    }

    [Fact]
    public void IsNewBoundary_SameSlotSameDay_ReturnsFalse()
    {
        // Arrange — repeated tick within the same slot must not re-segment
        var last = (new DateOnly(2026, 4, 28), 5);
        var current = (new DateOnly(2026, 4, 28), 5);

        // Act + Assert
        RecordingSlotCalculator.IsNewBoundary(current, last).Should().BeFalse();
    }

    [Fact]
    public void IsNewBoundary_MidnightRollover_ReturnsTrue()
    {
        // Arrange — last slot of yesterday, first slot of today
        var last = (new DateOnly(2026, 4, 28), 95);
        var current = (new DateOnly(2026, 4, 29), 0);

        // Act + Assert
        RecordingSlotCalculator.IsNewBoundary(current, last).Should().BeTrue();
    }

    [Fact]
    public void IsNewBoundary_ForwardClockSkip_ReturnsTrue()
    {
        // Arrange — NTP forward correction or DST spring-forward; a few
        // intermediate slots are skipped but the new slot is still later
        var last = (new DateOnly(2026, 4, 28), 5);
        var current = (new DateOnly(2026, 4, 28), 9);

        // Act + Assert
        RecordingSlotCalculator.IsNewBoundary(current, last).Should().BeTrue();
    }

    [Fact]
    public void IsNewBoundary_BackwardClockJumpSameDay_ReturnsFalse()
    {
        // Arrange — NTP backward correction or DST fall-back inside the
        // same day; we already processed this slot, do not segment again
        var last = (new DateOnly(2026, 4, 28), 11);
        var current = (new DateOnly(2026, 4, 28), 8);

        // Act + Assert
        RecordingSlotCalculator.IsNewBoundary(current, last).Should().BeFalse();
    }

    [Fact]
    public void IsNewBoundary_BackwardDate_ReturnsFalse()
    {
        // Arrange — clock somehow jumped to yesterday; do not segment
        var last = (new DateOnly(2026, 4, 28), 5);
        var current = (new DateOnly(2026, 4, 27), 95);

        // Act + Assert
        RecordingSlotCalculator.IsNewBoundary(current, last).Should().BeFalse();
    }

    [Theory]
    [InlineData(2026, 4, 28, 5, 2026, 4, 28, 5, 0)]
    [InlineData(2026, 4, 28, 5, 2026, 4, 28, 6, -1)]
    [InlineData(2026, 4, 28, 6, 2026, 4, 28, 5, 1)]
    [InlineData(2026, 4, 28, 95, 2026, 4, 29, 0, -1)]
    public void CompareSlot_OrdersLexicographically(
        int aYear,
        int aMonth,
        int aDay,
        int aSlot,
        int bYear,
        int bMonth,
        int bDay,
        int bSlot,
        int expectedSign)
    {
        // Arrange
        var a = (new DateOnly(aYear, aMonth, aDay), aSlot);
        var b = (new DateOnly(bYear, bMonth, bDay), bSlot);

        // Act
        var actual = RecordingSlotCalculator.CompareSlot(a, b);

        // Assert
        Math.Sign(actual).Should().Be(expectedSign);
    }
}