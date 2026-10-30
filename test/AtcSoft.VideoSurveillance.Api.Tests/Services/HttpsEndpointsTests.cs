namespace AtcSoft.VideoSurveillance.Api.Services;

/// <summary>
/// The server sends plain HTTP to HTTPS, and tells browsers to stay there,
/// only when it was given an HTTPS endpoint.
/// </summary>
public sealed class HttpsEndpointsTests
{
    [Fact]
    public void Plain_Http_Is_Not_Https()
        => HttpsEndpoints.IsConfigured(Configuration(("urls", "http://localhost:5000"))).Should().BeFalse();

    [Fact]
    public void Nothing_Configured_Is_Not_Https()
        => HttpsEndpoints.IsConfigured(Configuration()).Should().BeFalse();

    [Theory]
    [InlineData("https://+:5001")]
    [InlineData("http://localhost:5000;https://localhost:5001")]
    [InlineData("HTTPS://server.local:443")]
    public void An_Https_Address_In_Urls_Is_Https(string urls)
        => HttpsEndpoints.IsConfigured(Configuration(("urls", urls))).Should().BeTrue();

    [Fact]
    public void A_Kestrel_Endpoint_With_An_Https_Address_Is_Https()
        => HttpsEndpoints.IsConfigured(Configuration(
                ("Kestrel:Endpoints:Plain:Url", "http://*:5000"),
                ("Kestrel:Endpoints:Secure:Url", "https://*:5001")))
            .Should().BeTrue();

    [Fact]
    public void Kestrel_Endpoints_With_Plain_Addresses_Only_Are_Not_Https()
        => HttpsEndpoints.IsConfigured(Configuration(("Kestrel:Endpoints:Plain:Url", "http://*:5000"))).Should().BeFalse();

    [Fact]
    public void An_Https_Port_Behind_A_Proxy_Is_Https()
        => HttpsEndpoints.IsConfigured(Configuration(("https_port", "443"))).Should().BeTrue();

    private static IConfiguration Configuration(
        params (string Key, string Value)[] values)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(x => new KeyValuePair<string, string?>(x.Key, x.Value)))
            .Build();
}