namespace AtcSoft.VideoSurveillance.Api.Host;

/// <summary>
/// The calls that make the server reach out (a webhook test) are limited per
/// client address (x-ratelimit-*). Each test calls from an address of its own,
/// so it neither uses up nor meets the limit of the other host tests.
/// </summary>
[Collection(ApiHostTestGroup.Name)]
public sealed class ApiHostRateLimitTests(ApiHostFixture host)
{
    [Fact]
    public async Task The_Eleventh_Webhook_Test_In_A_Minute_Is_Turned_Away()
    {
        var address = IPAddress.Parse("192.0.2.51");
        var path = $"/webhooks/{Guid.NewGuid()}/test";

        for (var i = 0; i < 10; i++)
        {
            var allowed = await SendAsync(address, path);
            allowed.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        }

        var refused = await SendAsync(address, path);

        refused.Response.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
        refused.Response.Headers.RetryAfter.ToString().Should().NotBeEmpty();
        refused.Response.ContentType.Should().StartWith("application/problem+json");
    }

    [Fact]
    public async Task The_Twenty_First_Snapshot_In_Ten_Seconds_Is_Turned_Away()
    {
        var address = IPAddress.Parse("192.0.2.54");
        var path = $"/cameras/{Guid.NewGuid()}/snapshot";

        for (var i = 0; i < 20; i++)
        {
            var allowed = await SendAsync(address, path, HttpMethods.Get);
            allowed.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
        }

        var refused = await SendAsync(address, path, HttpMethods.Get);

        refused.Response.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
    }

    [Fact]
    public async Task Another_Address_Has_A_Limit_Of_Its_Own()
    {
        var path = $"/webhooks/{Guid.NewGuid()}/test";
        for (var i = 0; i < 11; i++)
        {
            await SendAsync(IPAddress.Parse("192.0.2.52"), path);
        }

        var other = await SendAsync(IPAddress.Parse("192.0.2.53"), path);

        other.Response.StatusCode.Should().Be(StatusCodes.Status404NotFound);
    }

    // Refresh tokens are random, so guessing them is not what the sign-in limit is for. Each
    // page load of the web client renews its session (the access token is kept in memory
    // only); under the sign-in limit the eleventh load in a minute from one address (an
    // office behind one address) was refused and the browser signed out. (Found by the E2E
    // smoke test.)
    [Fact]
    public async Task Renewing_Sessions_Is_Not_Held_To_The_Sign_In_Limit()
    {
        var address = IPAddress.Parse("192.0.2.61");

        for (var i = 0; i < 30; i++)
        {
            var renewal = await SendAnonymousJsonAsync(address, "/auth/refresh", """{"refreshToken":"not-a-token"}""");
            renewal.Response.StatusCode.Should().Be(StatusCodes.Status400BadRequest, $"renewal {i + 1} is answered, not turned away");
        }
    }

    [Fact]
    public async Task Renewing_Sessions_Has_A_Limit_Of_Its_Own()
    {
        var address = IPAddress.Parse("192.0.2.62");

        for (var i = 0; i < 60; i++)
        {
            await SendAnonymousJsonAsync(address, "/auth/refresh", """{"refreshToken":"not-a-token"}""");
        }

        var refused = await SendAnonymousJsonAsync(address, "/auth/refresh", """{"refreshToken":"not-a-token"}""");

        refused.Response.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
    }

    [Fact]
    public async Task The_Eleventh_Sign_In_In_A_Minute_Is_Turned_Away()
    {
        var address = IPAddress.Parse("192.0.2.63");

        for (var i = 0; i < 10; i++)
        {
            await SendAnonymousJsonAsync(address, "/auth/token", """{"userName":"nobody","password":"wrong-password"}""");
        }

        var refused = await SendAnonymousJsonAsync(address, "/auth/token", """{"userName":"nobody","password":"wrong-password"}""");

        refused.Response.StatusCode.Should().Be(StatusCodes.Status429TooManyRequests);
    }

    private Task<HttpContext> SendAnonymousJsonAsync(
        IPAddress address,
        string path,
        string json)
        => host.SendAsync(context =>
        {
            context.Request.Method = HttpMethods.Post;
            context.Request.Path = path;
            context.Request.ContentType = "application/json";
            context.Request.Body = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
            context.Connection.RemoteIpAddress = address;
        });

    private Task<HttpContext> SendAsync(
        IPAddress address,
        string path,
        string? method = null)
    {
        var token = host.GetAdminAccessToken();
        return host.SendAsync(context =>
        {
            context.Request.Method = method ?? HttpMethods.Post;
            context.Request.Path = path;
            context.Request.Headers.Authorization = "Bearer " + token;
            context.Connection.RemoteIpAddress = address;
        });
    }
}