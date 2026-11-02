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
}