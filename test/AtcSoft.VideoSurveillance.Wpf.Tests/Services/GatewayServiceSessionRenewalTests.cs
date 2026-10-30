namespace AtcSoft.VideoSurveillance.Wpf.Services;

/// <summary>
/// The client forgets its stored session only when the server refuses the refresh token
/// (400). A 429, a server error or no server keeps it, so a restarted or busy server does
/// not sign the desktop user out.
/// </summary>
public sealed class GatewayServiceSessionRenewalTests
{
    [Fact]
    public async Task A_Refused_Refresh_Token_Is_Reported_As_Refused()
    {
        using var body = new StringContent("""{"title":"Bad Request","status":400,"detail":"The session has ended; sign in again."}""");
        using var server = new GatewayTestServer(HttpStatusCode.BadRequest, body, "application/problem+json");

        var (session, outcome) = await server.Gateway.RefreshSessionAsync("old-token", TestContext.Current.CancellationToken);

        session.Should().BeNull();
        outcome.Should().Be(SessionRenewalOutcome.Refused);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task A_Busy_Or_Failing_Server_Is_Not_A_Refusal(
        HttpStatusCode status)
    {
        using var body = new StringContent($$"""{"title":"Busy","status":{{(int)status}}}""");
        using var server = new GatewayTestServer(status, body, "application/problem+json");

        var (session, outcome) = await server.Gateway.RefreshSessionAsync("token", TestContext.Current.CancellationToken);

        session.Should().BeNull();
        outcome.Should().Be(SessionRenewalOutcome.Unavailable);
    }

    [Fact]
    public async Task No_Server_Is_Not_A_Refusal()
    {
        using var server = new GatewayTestServer(_ => throw new HttpRequestException("No such host."));

        var (session, outcome) = await server.Gateway.RefreshSessionAsync("token", TestContext.Current.CancellationToken);

        session.Should().BeNull();
        outcome.Should().Be(SessionRenewalOutcome.Unavailable);
    }
}