namespace AtcSoft.VideoSurveillance.Wpf.Services;

/// <summary>
/// Instances of the client share the kept session of a server. A renewal sends
/// the kept refresh token (another instance may have renewed already); a busy
/// server keeps the session; a refused token is forgotten only while it is
/// still the kept one.
/// </summary>
public sealed class AuthServiceTests : IDisposable
{
    private static readonly Uri Server = new("http://server.test");

    private readonly string folder = Path.Combine(Path.GetTempPath(), "atcsoft-auth-" + Guid.NewGuid().ToString("N"));
    private readonly Microsoft.Extensions.Time.Testing.FakeTimeProvider time = new(DateTimeOffset.Parse("2026-10-15T12:00:00Z", CultureInfo.InvariantCulture));
    private readonly ClientSession session;
    private readonly SessionStore store;
    private readonly List<string> sent = [];

    public AuthServiceTests()
    {
        session = new ClientSession(time);
        store = new SessionStore(folder);
    }

    public void Dispose()
    {
        session.Dispose();
        if (Directory.Exists(folder))
        {
            foreach (var file in Directory.GetFiles(folder))
            {
                File.Delete(file);
            }

            Directory.Delete(folder);
        }
    }

    [Fact]
    public async Task A_Newer_Kept_Token_Is_The_One_Renewed_And_The_New_One_Is_Kept()
    {
        using var server = CreateServer(token => token == "newer" ? Renewed("newest") : Refused());
        _ = new AuthService(server.Gateway, session, store, Server);
        SignedIn("older");
        store.Save(Server, "newer", "admin");

        var renewed = await session.RenewAsync(TestContext.Current.CancellationToken, staleToken: "access-older");

        renewed.Should().BeTrue();
        sent.Should().Equal("newer");
        session.RefreshToken.Should().Be("newest");
        store.Load(Server)!.Value.RefreshToken.Should().Be("newest");
    }

    [Fact]
    public async Task A_Refused_Token_That_Is_Still_Kept_Is_Forgotten()
    {
        using var server = CreateServer(_ => Refused());
        _ = new AuthService(server.Gateway, session, store, Server);
        SignedIn("old");
        store.Save(Server, "old", "admin");

        var renewed = await session.RenewAsync(TestContext.Current.CancellationToken, staleToken: "access-old");

        renewed.Should().BeFalse();
        session.IsSignedIn.Should().BeFalse();
        store.Load(Server).Should().BeNull();
    }

    [Fact]
    public async Task A_Session_Another_Instance_Renewed_While_The_Call_Was_On_Its_Way_Is_Tried_And_Kept()
    {
        using var server = CreateServer(token =>
        {
            if (token == "old")
            {
                store.Save(Server, "other-instance", "admin");
                return Refused();
            }

            return token == "other-instance" ? Renewed("after-other-instance") : Refused();
        });
        _ = new AuthService(server.Gateway, session, store, Server);
        SignedIn("old");
        store.Save(Server, "old", "admin");

        var renewed = await session.RenewAsync(TestContext.Current.CancellationToken, staleToken: "access-old");

        renewed.Should().BeTrue();
        sent.Should().Equal("old", "other-instance");
        store.Load(Server)!.Value.RefreshToken.Should().Be("after-other-instance");
    }

    [Fact]
    public async Task A_Busy_Server_Keeps_The_Session_And_What_Is_Kept()
    {
        using var server = CreateServer(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        _ = new AuthService(server.Gateway, session, store, Server);
        SignedIn("token");
        store.Save(Server, "token", "admin");

        var renewed = await session.RenewAsync(TestContext.Current.CancellationToken, staleToken: "access-token");

        renewed.Should().BeFalse();
        session.IsSignedIn.Should().BeTrue();
        session.RefreshToken.Should().Be("token");
        store.Load(Server)!.Value.RefreshToken.Should().Be("token");
    }

    private static HttpResponseMessage Renewed(string refreshToken)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $$"""{"accessToken":"access-{{refreshToken}}","expiresInSeconds":3600,"refreshToken":"{{refreshToken}}","userName":"admin","role":"admin","mediaToken":"media"}""",
                System.Text.Encoding.UTF8,
                "application/json"),
        };

    private static HttpResponseMessage Refused()
        => new(HttpStatusCode.BadRequest)
        {
            Content = new StringContent(
                """{"title":"Bad Request","status":400,"detail":"The session has ended; sign in again."}""",
                System.Text.Encoding.UTF8,
                "application/problem+json"),
        };

    private void SignedIn(string refreshToken)
        => session.Apply(new Session(
            AccessToken: "access-" + refreshToken,
            ExpiresInSeconds: 3600,
            RefreshToken: refreshToken,
            UserName: "admin",
            Role: UserRole.Admin,
            MediaToken: "media"));

    private GatewayTestServer CreateServer(
        Func<string, HttpResponseMessage> answer)
        => new(request =>
        {
            using var body = JsonDocument.Parse(request.Content!.ReadAsStream());
            var token = body.RootElement.GetProperty("refreshToken").GetString()!;
            sent.Add(token);
            var response = answer(token);
            response.RequestMessage = request;
            return response;
        });
}