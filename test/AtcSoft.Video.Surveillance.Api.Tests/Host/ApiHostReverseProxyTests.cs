namespace AtcSoft.Video.Surveillance.Api.Host;

/// <summary>
/// Behind a proxy on the same machine, the limits are per client, not one for everyone behind
/// the proxy; a caller that is no trusted proxy cannot claim another client's address.
/// </summary>
[Collection(ApiHostTestGroup.Name)]
public sealed class ApiHostReverseProxyTests(ApiHostFixture host)
{
    [Fact]
    public async Task Clients_Behind_A_Proxy_On_This_Machine_Have_A_Limit_Each()
    {
        var path = $"/webhooks/{Guid.NewGuid()}/test";
        for (var i = 0; i < 11; i++)
        {
            await SendAsync(IPAddress.Loopback, "192.0.2.71", path);
        }

        var other = await SendAsync(IPAddress.Loopback, "192.0.2.72", path);

        other.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task A_Caller_That_Is_No_Proxy_Is_Held_To_Its_Own_Address()
    {
        var caller = IPAddress.Parse("192.0.2.73");
        var path = $"/webhooks/{Guid.NewGuid()}/test";
        for (var i = 0; i < 10; i++)
        {
            await SendAsync(caller, $"192.0.2.{100 + i}", path);
        }

        var refused = await SendAsync(caller, "192.0.2.120", path);

        refused.Response.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
    }

    private Task<HttpContext> SendAsync(
        IPAddress caller,
        string forwardedFor,
        string path)
    {
        var token = host.GetAdminAccessToken();
        return host.SendAsync(context =>
        {
            context.Request.Method = HttpMethods.Post;
            context.Request.Path = path;
            context.Request.Headers.Authorization = "Bearer " + token;
            context.Request.Headers["X-Forwarded-For"] = forwardedFor;
            context.Connection.RemoteIpAddress = caller;
        });
    }
}