namespace AtcSoft.VideoEngine.Windows.MediaFoundation;

public class UsbSymbolicLinkParserTests
{
    [Fact]
    public void Parse_LogitechBrio_ExtractsVidPid()
    {
        var (vid, pid) = UsbSymbolicLinkParser.Parse(
            @"\\?\usb#vid_046d&pid_085e&mi_00#7&15ee2c2&0&0000#{e5323777-f976-4f5b-9b55-b94699c46e44}");

        vid.Should().Be("046d");
        pid.Should().Be("085e");
    }

    [Fact]
    public void Parse_UpperCase_LowerCases_Hex()
    {
        var (vid, pid) = UsbSymbolicLinkParser.Parse(@"\\?\USB#VID_046D&PID_085E");

        vid.Should().Be("046d");
        pid.Should().Be("085e");
    }

    [Fact]
    public void Parse_NoVid_Returns_NullPair()
    {
        var (vid, pid) = UsbSymbolicLinkParser.Parse(@"\\?\display#cam");

        vid.Should().BeNull();
        pid.Should().BeNull();
    }

    [Fact]
    public void Parse_TruncatedVid_Returns_Null()
    {
        var (vid, _) = UsbSymbolicLinkParser.Parse("vid_04");

        vid.Should().BeNull();
    }

    [Fact]
    public void Parse_NonHexVid_Returns_Null()
    {
        var (vid, _) = UsbSymbolicLinkParser.Parse("vid_xyz1");

        vid.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Parse_NullOrEmpty_Returns_NullPair(string? input)
    {
        var (vid, pid) = UsbSymbolicLinkParser.Parse(input!);

        vid.Should().BeNull();
        pid.Should().BeNull();
    }
}