namespace Atc.Video.Engine.Helpers;

public sealed class FFmpegErrorTextTests
{
    [Theory]
    [InlineData(-138, "Connection timed out")]
    [InlineData(-107, "Connection refused")]
    [InlineData(-110, "No route to host")]
    [InlineData(-118, "Network is unreachable")]
    public void Describe_Names_A_Windows_Network_Error_FFmpeg_Leaves_Unnamed(
        int errorCode,
        string expected)
    {
        var text = FFmpegErrorText.Describe(errorCode, $"Error number {errorCode} occurred", isWindows: true);

        text.Should().Be(expected);
    }

    [Fact]
    public void Describe_Keeps_FFmpegs_Own_Text()
    {
        FFmpegErrorText.Describe(-5, "I/O error", isWindows: true).Should().Be("I/O error");
    }

    [Fact]
    public void Describe_Keeps_An_Unnamed_Number_It_Does_Not_Know()
    {
        FFmpegErrorText.Describe(-999, "Error number -999 occurred", isWindows: true)
            .Should().Be("Error number -999 occurred");
    }

    [Fact]
    public void Describe_Does_Not_Read_The_Windows_Numbers_On_Other_Systems()
    {
        // 110 is ETIMEDOUT on Linux, not "No route to host".
        FFmpegErrorText.Describe(-110, "Error number -110 occurred", isWindows: false)
            .Should().Be("Error number -110 occurred");
    }
}