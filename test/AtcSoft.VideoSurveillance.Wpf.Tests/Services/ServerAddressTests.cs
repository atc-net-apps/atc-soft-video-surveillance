namespace AtcSoft.VideoSurveillance.Wpf.Services;

/// <summary>
/// A server with HTTPS redirects plain HTTP to its https:// address, and an HTTP client drops
/// the Authorization header when the scheme changes, so every signed-in call answered 401.
/// The client asks once at start and uses the address the server redirects to.
/// </summary>
public sealed class ServerAddressTests
{
    [Fact]
    public async Task A_Redirect_To_Https_Gives_The_Https_Address()
    {
        using var handler = new AnswerFrom("https://server.test:5001/auth/setup");
        using var client = new HttpClient(handler);

        var resolved = await ServerAddress.ResolveAsync(client, new Uri("http://server.test:5000"), TestContext.Current.CancellationToken);

        resolved.Should().Be(new Uri("https://server.test:5001"));
    }

    [Fact]
    public async Task A_Server_Without_A_Redirect_Keeps_Its_Address()
    {
        using var handler = new AnswerFrom("http://server.test:5000/auth/setup");
        using var client = new HttpClient(handler);

        var resolved = await ServerAddress.ResolveAsync(client, new Uri("http://server.test:5000"), TestContext.Current.CancellationToken);

        resolved.Should().Be(new Uri("http://server.test:5000"));
    }

    [Fact]
    public async Task An_Https_Address_Is_Not_Asked_About()
    {
        using var handler = new AnswerFrom("https://elsewhere.test/auth/setup");
        using var client = new HttpClient(handler);

        var resolved = await ServerAddress.ResolveAsync(client, new Uri("https://server.test:5001"), TestContext.Current.CancellationToken);

        resolved.Should().Be(new Uri("https://server.test:5001"));
        handler.Calls.Should().Be(0);
    }

    [Fact]
    public async Task A_Server_That_Cannot_Be_Reached_Keeps_Its_Address()
    {
        using var handler = new AnswerFrom(null);
        using var client = new HttpClient(handler);

        var resolved = await ServerAddress.ResolveAsync(client, new Uri("http://server.test:5000"), TestContext.Current.CancellationToken);

        resolved.Should().Be(new Uri("http://server.test:5000"));
    }

    /// <summary>
    /// Answers as if the request had ended at the given address (after the redirects the
    /// real handler follows), or fails when there is none.
    /// </summary>
    private sealed class AnswerFrom(string? finalAddress) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Calls++;
            if (finalAddress is null)
            {
                throw new HttpRequestException("No connection could be made.");
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                RequestMessage = new HttpRequestMessage(HttpMethod.Get, finalAddress),
                Content = new StringContent("""{"setupRequired":false}"""),
            });
        }
    }
}