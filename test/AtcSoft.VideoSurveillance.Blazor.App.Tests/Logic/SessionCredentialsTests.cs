namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// Every call to the server carries the access token; one that is about to run
/// out is renewed first, and a read the server refused for its token is sent
/// again after a renewal.
/// </summary>
public sealed class SessionCredentialsTests : IDisposable
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-15T12:00:00Z", CultureInfo.InvariantCulture);

    private readonly Microsoft.Extensions.Time.Testing.FakeTimeProvider time = new(Now);
    private readonly BrowserSession session;

    public SessionCredentialsTests()
        => session = new BrowserSession(time);

    public void Dispose()
        => session.Dispose();

    [Fact]
    public async Task A_Call_Carries_The_Access_Token()
    {
        session.Apply(Session("first", expiresIn: 3600));
        var sent = new List<HttpRequestMessage>();
        using var invoker = CreateInvoker(request =>
        {
            sent.Add(request);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        });

        await CallAsync(invoker, HttpMethod.Get, "http://server.test/cameras");

        sent.Single().Headers.Authorization!.ToString().Should().Be("Bearer first");
    }

    [Fact]
    public async Task A_Token_About_To_Run_Out_Is_Renewed_Before_The_Call()
    {
        session.Apply(Session("old", expiresIn: 3600));
        session.Renew = _ =>
        {
            session.Apply(Session("new", expiresIn: 3600));
            return Task.FromResult(SessionRenewalOutcome.Renewed);
        };
        time.Advance(TimeSpan.FromMinutes(59.5));
        var sent = new List<string?>();
        using var invoker = CreateInvoker(request =>
        {
            sent.Add(request.Headers.Authorization?.Parameter);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        });

        await CallAsync(invoker, HttpMethod.Get, "http://server.test/cameras");

        sent.Should().Equal("new");
    }

    [Fact]
    public async Task A_Read_Refused_For_Its_Token_Is_Sent_Again_After_A_Renewal()
    {
        session.Apply(Session("stale", expiresIn: 3600));
        session.Renew = _ =>
        {
            session.Apply(Session("fresh", expiresIn: 3600));
            return Task.FromResult(SessionRenewalOutcome.Renewed);
        };
        var sent = new List<string?>();
        using var invoker = CreateInvoker(request =>
        {
            sent.Add(request.Headers.Authorization?.Parameter);
            return new HttpResponseMessage(sent.Count == 1 ? System.Net.HttpStatusCode.Unauthorized : System.Net.HttpStatusCode.OK);
        });

        var response = await CallAsync(invoker, HttpMethod.Get, "http://server.test/cameras");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        sent.Should().Equal("stale", "fresh");
    }

    [Fact]
    public async Task Two_Reads_Refused_For_The_Same_Token_Renew_Once()
    {
        session.Apply(Session("stale", expiresIn: 3600));
        var renewals = 0;
        session.Renew = _ =>
        {
            renewals++;
            session.Apply(Session("fresh" + renewals, expiresIn: 3600));
            return Task.FromResult(SessionRenewalOutcome.Renewed);
        };
        using var bothSent = new Barrier(2);
        using var invoker = CreateInvoker(request =>
        {
            if (request.Headers.Authorization?.Parameter == "stale")
            {
                bothSent.SignalAndWait(TimeSpan.FromSeconds(5));
                return new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized);
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        });

        await Task.WhenAll(
            Task.Run(() => CallAsync(invoker, HttpMethod.Get, "http://server.test/cameras")),
            Task.Run(() => CallAsync(invoker, HttpMethod.Get, "http://server.test/cameras")));

        renewals.Should().Be(1);
    }

    [Fact]
    public async Task A_Read_Refused_After_Another_Call_Renewed_Does_Not_Renew_Again()
    {
        session.Apply(Session("stale", expiresIn: 3600));
        var renewals = 0;
        session.Renew = _ =>
        {
            renewals++;
            session.Apply(Session("fresh" + renewals, expiresIn: 3600));
            return Task.FromResult(SessionRenewalOutcome.Renewed);
        };
        var staleAnswers = 0;
        using var invoker = CreateInvoker(request =>
        {
            if (request.Headers.Authorization?.Parameter == "stale" && Interlocked.Increment(ref staleAnswers) == 2)
            {
                // The second read's 401 arrives after the first read has renewed.
                SpinWait.SpinUntil(() => Volatile.Read(ref renewals) == 1, TimeSpan.FromSeconds(5));
            }

            return new HttpResponseMessage(request.Headers.Authorization?.Parameter == "stale" ? System.Net.HttpStatusCode.Unauthorized : System.Net.HttpStatusCode.OK);
        });

        await Task.WhenAll(
            Task.Run(() => CallAsync(invoker, HttpMethod.Get, "http://server.test/cameras")),
            Task.Run(() => CallAsync(invoker, HttpMethod.Get, "http://server.test/cameras")));

        renewals.Should().Be(1);
    }

    [Fact]
    public async Task A_Session_That_Cannot_Be_Renewed_Ends()
    {
        session.Apply(Session("stale", expiresIn: 3600));
        session.Renew = _ => Task.FromResult(SessionRenewalOutcome.Refused);
        using var invoker = CreateInvoker(_ => new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized));

        var response = await CallAsync(invoker, HttpMethod.Get, "http://server.test/cameras");

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.Unauthorized);
        session.IsSignedIn.Should().BeFalse("the user signs in again");
    }

    [Fact]
    public async Task The_Sign_In_Calls_Go_Without_A_Token()
    {
        session.Apply(Session("first", expiresIn: 3600));
        var sent = new List<HttpRequestMessage>();
        using var invoker = CreateInvoker(request =>
        {
            sent.Add(request);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK);
        });

        await CallAsync(invoker, HttpMethod.Post, "http://server.test/auth/refresh", ApiOperations.RefreshSession, ApiOperationSecurities.RefreshSession);

        sent.Single().Headers.Authorization.Should().BeNull();
    }

    private static Session Session(
        string accessToken,
        int expiresIn)
        => new(
            AccessToken: accessToken,
            ExpiresInSeconds: expiresIn,
            RefreshToken: "refresh-" + accessToken,
            UserName: "admin",
            Role: UserRole.Admin,
            MediaToken: "media-" + accessToken);

    private static async Task<HttpResponseMessage> CallAsync(
        HttpMessageInvoker invoker,
        HttpMethod method,
        string url,
        string operation = ApiOperations.ListCameras,
        ApiOperationSecurity? security = null)
    {
        using var request = new HttpRequestMessage(method, url);
        ApiRequest.Stamp(request, operation, security ?? ApiOperationSecurities.ListCameras);
        return await invoker.SendAsync(request, TestContext.Current.CancellationToken).ConfigureAwait(false);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The invoker owns the handlers (disposeHandler) and the test disposes the invoker.")]
    private HttpMessageInvoker CreateInvoker(
        Func<HttpRequestMessage, HttpResponseMessage> answer)
    {
        var credentials = new ApiCredentials();
        SessionCredentials.Configure(credentials, session);
        return new(new ApiCredentialsHandler(credentials) { InnerHandler = new AnswerHandler(answer) }, disposeHandler: true);
    }

    private sealed class AnswerHandler(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(answer(request));
    }
}