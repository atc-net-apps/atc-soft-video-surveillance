namespace AtcSoft.VideoSurveillance.Factories;

public class DropDownItemsFactoryTests
{
    [Theory]
    [InlineData("360p", 360)]
    [InlineData("480p", 480)]
    [InlineData("720p", 720)]
    [InlineData("1080p", 1080)]
    [InlineData("Auto", 0)]
    [InlineData("Unknown", 0)]
    public void GetMaxResolutionFromQuality_Returns_Expected_Value(
        string quality,
        int expected)
    {
        // Act
        var result = DropDownItemsFactory.GetMaxResolutionFromQuality(quality);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData("320x240", 320, 240)]
    [InlineData("640x480", 640, 480)]
    [InlineData("800x600", 800, 600)]
    public void ParseAnalysisResolution_Valid_Input_Returns_Parsed_Values(
        string resolution,
        int expectedWidth,
        int expectedHeight)
    {
        // Act
        var (width, height) = DropDownItemsFactory.ParseAnalysisResolution(resolution);

        // Assert
        width.Should().Be(expectedWidth);
        height.Should().Be(expectedHeight);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid")]
    public void ParseAnalysisResolution_Invalid_Input_Returns_Default(
        string? resolution)
    {
        // Act
        var (width, height) = DropDownItemsFactory.ParseAnalysisResolution(resolution);

        // Assert
        width.Should().Be(320);
        height.Should().Be(240);
    }

    [Fact]
    public void FormatAnalysisResolution_Returns_Expected_Format()
    {
        // Act
        var result = DropDownItemsFactory.FormatAnalysisResolution(640, 480);

        // Assert
        result.Should().Be("640x480");
    }

    [Theory]
    [InlineData("10s", 10)]
    [InlineData("30s", 30)]
    [InlineData("1m", 60)]
    [InlineData("5m", 300)]
    [InlineData("1h", 3600)]
    [InlineData("24h", 86400)]
    public void ParseTimelapseInterval_Returns_Expected_TimeSpan(
        string interval,
        double expectedSeconds)
    {
        // Act
        var result = DropDownItemsFactory.ParseTimelapseInterval(interval);

        // Assert
        result.TotalSeconds.Should().Be(expectedSeconds);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("invalid")]
    public void ParseTimelapseInterval_Invalid_Input_Returns_Default(
        string? interval)
    {
        // Act
        var result = DropDownItemsFactory.ParseTimelapseInterval(interval);

        // Assert
        result.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void VideoQualityItems_Is_Not_Empty()
    {
        // Assert
        DropDownItemsFactory.VideoQualityItems.Should().NotBeEmpty();
        DropDownItemsFactory.VideoQualityItems.Should().ContainKey("Auto");
    }

    [Fact]
    public void ProtocolItems_Contains_Expected_Entries()
    {
        // Assert
        DropDownItemsFactory.ProtocolItems.Should().ContainKey("Rtsp");
        DropDownItemsFactory.ProtocolItems.Should().ContainKey("Http");
    }
}