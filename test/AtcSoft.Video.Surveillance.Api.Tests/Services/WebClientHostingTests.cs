namespace AtcSoft.Video.Surveillance.Api.Services;

/// <summary>
/// The web client's pages share addresses with the API: a browser loading a page gets the page,
/// everything else the API.
/// </summary>
public sealed class WebClientHostingTests
{
    [Theory]
    [InlineData("/")]
    [InlineData("/cameras")]
    [InlineData("/recordings")]
    [InlineData("/live")]
    public void A_Browser_Loading_A_Page_Gets_The_Page(string path)
        => WebClientHosting.IsPageLoad(Request("GET", path, mode: "navigate", accept: "text/html,*/*")).Should().BeTrue();

    [Theory]
    [InlineData("/cameras", "cors")]
    [InlineData("/cameras", "same-origin")]
    [InlineData("/recordings", "no-cors")]
    public void A_Page_Fetching_The_Api_Gets_The_Api(
        string path,
        string mode)
        => WebClientHosting.IsPageLoad(Request("GET", path, mode: mode, accept: "application/json")).Should().BeFalse();

    [Fact]
    public void A_Client_Without_Browser_Headers_Gets_The_Api()
        => WebClientHosting.IsPageLoad(Request("GET", "/cameras", mode: null, accept: "application/json")).Should().BeFalse();

    [Fact]
    public void An_Old_Browser_Asking_For_Html_Gets_The_Page()
        => WebClientHosting.IsPageLoad(Request("GET", "/cameras", mode: null, accept: "text/html,application/xhtml+xml")).Should().BeTrue();

    [Fact]
    public void A_Change_Is_Never_A_Page_Load()
        => WebClientHosting.IsPageLoad(Request("POST", "/cameras", mode: "navigate", accept: "text/html")).Should().BeFalse();

    [Theory]
    [InlineData("/streams/0f8fad5bd9cb469fa16570867728950e/index.m3u8")]
    [InlineData("/recordings-files/Garage/clip")]
    [InlineData("/events")]
    [InlineData("/health")]
    [InlineData("/openapi/v1.json")]
    [InlineData("/api-reference")]
    [InlineData("/_framework/blazor.webassembly.js")]
    [InlineData("/css/app.css")]
    public void Files_The_Server_Serves_Itself_Are_Never_The_Page(string path)
        => WebClientHosting.IsPageLoad(Request("GET", path, mode: "navigate", accept: "text/html")).Should().BeFalse();

    [Fact]
    public void The_Web_Client_Is_On_By_Default()
        => WebClientHosting.IsEnabled(Configuration()).Should().BeTrue();

    [Fact]
    public void The_Web_Client_Can_Be_Turned_Off()
        => WebClientHosting.IsEnabled(Configuration((WebClientHosting.EnabledKey, "false"))).Should().BeFalse();

    private static HttpRequest Request(
        string method,
        string path,
        string? mode,
        string accept)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Request.Headers.Accept = accept;
        if (mode is not null)
        {
            context.Request.Headers["Sec-Fetch-Mode"] = mode;
        }

        return context.Request;
    }

    private static IConfiguration Configuration(
        params (string Key, string Value)[] values)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(x => new KeyValuePair<string, string?>(x.Key, x.Value)))
            .Build();
}