namespace AtcSoft.Video.Surveillance.Api.Services;

/// <summary>
/// The server believes a proxy's word about the client only from a proxy it trusts: this machine,
/// and the addresses and networks it is given.
/// </summary>
public sealed class ReverseProxyTests
{
    [Fact]
    public void Nothing_Configured_Trusts_This_Machine_Only()
    {
        var options = Configure();

        options.KnownProxies.Should().Equal(IPAddress.IPv6Loopback);
        options.KnownIPNetworks.Should().Equal(System.Net.IPNetwork.Parse("127.0.0.0/8"));
        options.ForwardedHeaders.Should().Be(ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedPrefix);
    }

    [Fact]
    public void Proxies_And_Networks_From_A_List_Are_Trusted()
    {
        var options = Configure(
            ("ReverseProxy:KnownProxies:0", "192.168.1.10"),
            ("ReverseProxy:KnownNetworks:0", "172.16.0.0/12"),
            ("ReverseProxy:KnownNetworks:1", "10.0.0.0/8"));

        options.KnownProxies.Should().Contain(IPAddress.Parse("192.168.1.10"));
        options.KnownIPNetworks.Should().Contain(System.Net.IPNetwork.Parse("172.16.0.0/12"));
        options.KnownIPNetworks.Should().Contain(System.Net.IPNetwork.Parse("10.0.0.0/8"));
    }

    [Fact]
    public void Networks_In_One_Value_Are_Trusted()
    {
        var options = Configure(("ReverseProxy:KnownNetworks", "172.16.0.0/12; 192.168.0.0/16"));

        options.KnownIPNetworks.Should().Contain(System.Net.IPNetwork.Parse("172.16.0.0/12"));
        options.KnownIPNetworks.Should().Contain(System.Net.IPNetwork.Parse("192.168.0.0/16"));
    }

    [Theory]
    [InlineData("ReverseProxy:KnownProxies", "proxy.local")]
    [InlineData("ReverseProxy:KnownNetworks", "172.16.0.0")]
    public void A_Value_That_Is_No_Address_Stops_The_Server_With_The_Value(
        string key,
        string value)
    {
        var act = () => Configure((key, value));

        act.Should().Throw<InvalidOperationException>().WithMessage($"*{value}*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("/")]
    public void Without_A_Path_The_Server_Is_At_The_Root(string? value)
        => ReverseProxy.PathBase(Settings((ReverseProxy.PathBaseKey, value))).Should().BeNull();

    [Theory]
    [InlineData("/surveillance")]
    [InlineData("surveillance")]
    [InlineData("/surveillance/")]
    public void A_Path_Has_One_Leading_Slash_And_No_Trailing_One(string value)
        => ReverseProxy.PathBase(Settings((ReverseProxy.PathBaseKey, value))).Should().Be(new PathString("/surveillance"));

    [Fact]
    public void An_Address_Is_No_Path()
    {
        var act = () => ReverseProxy.PathBase(Settings((ReverseProxy.PathBaseKey, "https://nas/surveillance")));

        act.Should().Throw<InvalidOperationException>().WithMessage("*https://nas/surveillance*");
    }

    private static IConfiguration Settings(
        params (string Key, string? Value)[] values)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(x => new KeyValuePair<string, string?>(x.Key, x.Value)))
            .Build();

    private static ForwardedHeadersOptions Configure(
        params (string Key, string Value)[] values)
    {
        var options = new ForwardedHeadersOptions();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(x => new KeyValuePair<string, string?>(x.Key, x.Value)))
            .Build();

        ReverseProxy.Configure(options, configuration);
        return options;
    }
}