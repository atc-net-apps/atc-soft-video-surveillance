namespace AtcSoft.Video.Surveillance.Api.Security;

/// <summary>
/// The first admin needs no setup code from a browser on the server's own machine, and only
/// there: never from another address, and never through a proxy, which connects from the
/// server's machine too.
/// </summary>
public sealed class LocalSetupTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    [InlineData("::ffff:127.0.0.1")]
    public void A_Browser_On_The_Server_Is_Local(string address)
        => LocalSetup.IsLocal(Request(address)).Should().BeTrue();

    [Theory]
    [InlineData("192.168.1.20")]
    [InlineData("172.17.0.1")]
    [InlineData("::ffff:10.0.0.5")]
    public void Another_Machine_Is_Not_Local(string address)
        => LocalSetup.IsLocal(Request(address)).Should().BeFalse();

    [Fact]
    public void A_Request_Without_An_Address_Is_Not_Local()
        => LocalSetup.IsLocal(new DefaultHttpContext()).Should().BeFalse();

    [Theory]
    [InlineData("X-Forwarded-For", "192.168.1.20")]
    [InlineData("X-Forwarded-Proto", "https")]
    [InlineData("X-Forwarded-Host", "cameras.example.com")]
    [InlineData("X-Original-For", "127.0.0.1:50000")]
    [InlineData("Forwarded", "for=192.168.1.20")]
    public void A_Proxy_On_The_Server_Is_Not_Local(
        string header,
        string value)
        => LocalSetup.IsLocal(Request("127.0.0.1", (header, value))).Should().BeFalse();

    [Fact]
    public async Task A_Proxied_Request_Is_Not_Local_After_The_Server_Has_Read_The_Proxys_Headers()
    {
        var options = new ForwardedHeadersOptions();
        ReverseProxy.Configure(options, new ConfigurationBuilder().Build());
        var context = Request("127.0.0.1", ("X-Forwarded-For", "127.0.0.1"), ("X-Forwarded-Proto", "https"));
        var middleware = new ForwardedHeadersMiddleware(
            _ => Task.CompletedTask,
            NullLoggerFactory.Instance,
            Microsoft.Extensions.Options.Options.Create(options));

        await middleware.Invoke(context);

        context.Connection.RemoteIpAddress.Should().Be(IPAddress.Loopback, "the proxy said the browser was on the server too");
        LocalSetup.IsLocal(context).Should().BeFalse();
    }

    [Fact]
    public void Turned_Off_Nothing_Is_Local()
        => new LocalSetup(allowed: false).Applies(Request("127.0.0.1")).Should().BeFalse();

    [Fact]
    public void Turned_On_A_Local_Request_Needs_No_Code()
        => new LocalSetup(allowed: true).Applies(Request("127.0.0.1")).Should().BeTrue();

    [Fact]
    public void Outside_A_Request_Nothing_Is_Local()
        => new LocalSetup(allowed: true).Applies(context: null).Should().BeFalse();

    private static DefaultHttpContext Request(
        string address,
        params (string Name, string Value)[] headers)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(address);
        foreach (var (name, value) in headers)
        {
            context.Request.Headers[name] = value;
        }

        return context;
    }
}