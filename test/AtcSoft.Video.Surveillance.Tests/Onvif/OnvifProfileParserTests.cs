namespace AtcSoft.Video.Surveillance.Helpers;

public sealed class OnvifProfileParserTests
{
    [Fact]
    public void The_Media_Service_Is_Read_From_The_Capabilities()
        => OnvifProfileParser.ParseMediaAddress(OnvifResponses.Capabilities)
            .Should().Be(new Uri("http://10.0.0.99:80/onvif/Media"));

    [Fact]
    public void The_Video_Profiles_Are_Read_With_Their_Encoding_And_Size()
    {
        var profiles = OnvifProfileParser.ParseProfiles(OnvifResponses.HikvisionProfiles);

        profiles.Select(p => (p.Token, p.Name, p.Encoding, p.Width, p.Height)).Should().Equal(
            [
                ("Profile_1", "mainStream", "H264", 2560, 1440),
                ("Profile_2", "subStream", "H264", 640, 480),
                ("Profile_3", "thirdStream", "H264", 1280, 720),
            ],
            "a profile without a video encoder (audio only) is left out");
    }

    [Fact]
    public void Other_Prefixes_And_Namespaces_Are_Read_Too()
        => OnvifProfileParser.ParseProfiles(OnvifResponses.MjpegOnlyProfiles)
            .Should().ContainSingle().Which.Encoding.Should().Be("JPEG");

    [Fact]
    public void The_Stream_Address_Is_Read_With_Its_Query()
        => OnvifProfileParser.ParseStreamUri(OnvifResponses.StreamUri("rtsp://10.0.0.99:554/cam/realmonitor?channel=1&subtype=1"))!
            .PathAndQuery.Should().Be("/cam/realmonitor?channel=1&subtype=1");

    [Fact]
    public void A_Refused_Sign_In_Is_Recognised()
    {
        OnvifProfileParser.IsNotAuthorized(OnvifResponses.NotAuthorizedFault).Should().BeTrue();
        OnvifProfileParser.IsNotAuthorized(OnvifResponses.HikvisionProfiles).Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("<not xml")]
    [InlineData("<html><body>Login</body></html>")]
    public void An_Answer_That_Is_No_Onvif_Answer_Gives_Nothing(string text)
    {
        OnvifProfileParser.ParseProfiles(text).Should().BeEmpty();
        OnvifProfileParser.ParseMediaAddress(text).Should().BeNull();
        OnvifProfileParser.ParseStreamUri(text).Should().BeNull();
    }
}