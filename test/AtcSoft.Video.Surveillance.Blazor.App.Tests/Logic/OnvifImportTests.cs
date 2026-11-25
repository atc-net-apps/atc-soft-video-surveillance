namespace AtcSoft.Video.Surveillance.Blazor.App.Logic;

public sealed class OnvifImportTests
{
    [Fact]
    public void A_Search_Says_What_It_Found_Or_Why_It_Could_Not_Search()
    {
        OnvifImport.Describe(new OnvifDiscoveryResult([], CouldProbe: false)).Should().Be(Translations.OnvifNoMulticast);
        OnvifImport.Describe(new OnvifDiscoveryResult([], CouldProbe: true)).Should().Be(Translations.OnvifNoneFound);
        OnvifImport.Describe(new OnvifDiscoveryResult([Device()], CouldProbe: true))
            .Should().Be(string.Format(CultureInfo.CurrentCulture, Translations.OnvifFound1, 1));
    }

    [Fact]
    public void A_Device_Is_Listed_With_Its_Name_Model_And_Address()
    {
        OnvifImport.Describe(Device()).Should().Be("Front Door · DS-2CD · 192.168.1.64");
        OnvifImport.Describe(new OnvifDiscoveredDevice("192.168.1.65", 80, string.Empty, string.Empty)).Should().Be("192.168.1.65");
    }

    [Fact]
    public void A_Read_Fills_In_The_Streams()
    {
        var read = new OnvifProfilesResult(OnvifProfilesResultOutcome.Read, [])
        {
            Port = 554,
            Path = "/Streaming/Channels/101",
            SubStreamPath = "/Streaming/Channels/102",
            SubStreamPort = 8554,
        };

        OnvifImport.Fields(read).Should().Be((554, "/Streaming/Channels/101", "/Streaming/Channels/102", 8554));
        OnvifImport.Describe(read).Should().Be(Translations.OnvifReadWithSubStream);
    }

    [Fact]
    public void A_Read_Without_A_Sub_Stream_Clears_It()
        => OnvifImport.Fields(new OnvifProfilesResult(OnvifProfilesResultOutcome.Read, []) { Path = "/live" })
            .Should().Be((554, "/live", string.Empty, 0));

    [Fact]
    public void A_Read_That_Failed_Fills_In_Nothing_And_Says_Why()
    {
        var refused = new OnvifProfilesResult(OnvifProfilesResultOutcome.Unauthorized, []);

        OnvifImport.Fields(refused).Should().BeNull();
        OnvifImport.Describe(refused).Should().Be(Translations.OnvifUnauthorized);
        OnvifImport.Describe(new OnvifProfilesResult(new OnvifProfilesResultOutcome("somethingNewer"), [])).Should().Be(Translations.OnvifNotOnvif);
    }

    private static OnvifDiscoveredDevice Device()
        => new("192.168.1.64", 80, "Front Door", "DS-2CD");
}