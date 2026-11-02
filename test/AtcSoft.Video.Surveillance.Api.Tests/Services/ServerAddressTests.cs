namespace AtcSoft.Video.Surveillance.Api.Services;

/// <summary>
/// The server listens on port 8585 unless it was given an address of its own.
/// </summary>
public sealed class ServerAddressTests
{
    [Fact]
    public void The_Default_Address_Is_Port_8585()
        => new Uri(ServerAddress.DefaultUrl).Port.Should().Be(8585);

    [Fact]
    public void Nothing_Configured_Takes_The_Default()
        => ServerAddress.IsConfigured(Configuration()).Should().BeFalse();

    [Theory]
    [InlineData("urls", "http://+:9000")]
    [InlineData("http_ports", "8080")]
    [InlineData("https_ports", "8443")]
    [InlineData("Kestrel:Endpoints:Plain:Url", "http://*:9000")]
    public void An_Address_Of_Its_Own_Is_Kept(
        string key,
        string value)
        => ServerAddress.IsConfigured(Configuration((key, value))).Should().BeTrue();

    private static IConfiguration Configuration(
        params (string Key, string Value)[] values)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(x => new KeyValuePair<string, string?>(x.Key, x.Value)))
            .Build();
}