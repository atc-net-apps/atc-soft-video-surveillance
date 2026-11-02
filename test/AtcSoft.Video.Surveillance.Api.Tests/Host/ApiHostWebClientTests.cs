namespace AtcSoft.Video.Surveillance.Api.Host;

/// <summary>
/// The server serves its web client from its own address, and an API call to an address the
/// client also has as a page still reaches the API.
/// </summary>
[Collection(ApiHostTestGroup.Name)]
public sealed class ApiHostWebClientTests(ApiHostFixture host)
{
    [Fact]
    public async Task An_Api_Call_To_An_Address_The_Web_Client_Also_Has_Gets_The_Api()
    {
        using var client = host.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/cameras", UriKind.Relative));
        request.Headers.Add("Sec-Fetch-Mode", "cors");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task A_Browser_Loading_A_Page_Gets_The_Web_Client()
    {
        using var client = host.CreateAnonymousClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/cameras", UriKind.Relative));
        request.Headers.Add("Sec-Fetch-Mode", "navigate");
        request.Headers.Accept.ParseAdd("text/html");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");
        var page = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        page.Should().Contain("_framework/blazor.webassembly.js");
        response.Headers.CacheControl?.NoStore.Should().BeTrue();
    }

    [Fact]
    public async Task Behind_A_Proxy_Under_A_Path_The_Page_Lives_Below_It()
    {
        var context = await host.SendAsync(c =>
        {
            c.Request.Method = HttpMethods.Get;
            c.Request.Path = "/live";
            c.Request.Headers["Sec-Fetch-Mode"] = "navigate";
            c.Request.Headers.Accept = "text/html";
            c.Request.Headers["X-Forwarded-Prefix"] = "/surveillance";
            c.Connection.RemoteIpAddress = IPAddress.Loopback;
        });

        context.Response.StatusCode.Should().Be(StatusCodes.Status200OK);
        using var reader = new StreamReader(context.Response.Body);
        var page = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
        page.Should().Contain("<base href=\"/surveillance/\" />");
    }

    [Fact]
    public async Task A_Compressed_File_Of_The_Web_Client_Varies_With_What_The_Browser_Accepts()
    {
        using var client = host.CreateAnonymousClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri("/_framework/blazor.webassembly.js", UriKind.Relative));
        request.Headers.AcceptEncoding.ParseAdd("gzip");

        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Vary.Should().NotContain("Content-Encoding");
        if (response.Content.Headers.ContentEncoding.Count > 0)
        {
            response.Headers.Vary.Should().Contain("Accept-Encoding");
        }
    }
}