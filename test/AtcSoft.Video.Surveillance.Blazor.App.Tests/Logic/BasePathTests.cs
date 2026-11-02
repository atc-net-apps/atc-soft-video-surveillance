namespace AtcSoft.Video.Surveillance.Blazor.App.Logic;

/// <summary>
/// The server under a path of a proxy's site: the API's requests stay below that path.
/// </summary>
public sealed class BasePathTests
{
    private static readonly Uri UnderAPath = new("https://nas.local/surveillance/");

    [Fact]
    public void A_Request_That_Lost_The_Path_Gets_It_Back()
        => BasePath.Keep(UnderAPath, new Uri("https://nas.local/cameras/42?x=1"))
            .Should().Be(new Uri("https://nas.local/surveillance/cameras/42?x=1"));

    [Fact]
    public void A_Request_Below_The_Path_Is_Kept()
        => BasePath.Keep(UnderAPath, new Uri("https://nas.local/surveillance/cameras"))
            .Should().Be(new Uri("https://nas.local/surveillance/cameras"));

    [Fact]
    public void At_The_Root_Nothing_Changes()
        => BasePath.Keep(new Uri("http://server:8585/"), new Uri("http://server:8585/cameras"))
            .Should().Be(new Uri("http://server:8585/cameras"));

    [Fact]
    public void A_Path_That_Only_Starts_Like_The_Base_Gets_It()
        => BasePath.Keep(UnderAPath, new Uri("https://nas.local/surveillance-old/x"))
            .Should().Be(new Uri("https://nas.local/surveillance/surveillance-old/x"));

    [Fact]
    public void Another_Server_Is_Left_Alone()
        => BasePath.Keep(UnderAPath, new Uri("https://other.example/cameras"))
            .Should().Be(new Uri("https://other.example/cameras"));
}