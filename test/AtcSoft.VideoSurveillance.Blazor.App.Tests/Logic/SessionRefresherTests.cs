namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// Every tab of the site shares the kept session. A renewal sends the kept
/// refresh token (another tab may have renewed already), and a refused one is
/// forgotten only while it is still the kept one.
/// </summary>
public sealed class SessionRefresherTests
{
    private readonly FakeStorage storage = new();
    private readonly List<string> sent = [];

    [Fact]
    public async Task A_Newer_Kept_Token_Is_The_One_Renewed()
    {
        storage.Value = StoredSession.Write("newer", "admin");
        var sut = CreateSut(token => token == "newer" ? Renewed("newest") : Refused());

        var (session, outcome) = await sut.RefreshAsync("older", TestContext.Current.CancellationToken);

        outcome.Should().Be(SessionRenewalOutcome.Renewed);
        session!.RefreshToken.Should().Be("newest");
        sent.Should().Equal("newer");
        StoredSession.Read(storage.Value)!.RefreshToken.Should().Be("newest");
    }

    [Fact]
    public async Task Without_A_Kept_Token_The_Tabs_Own_Is_Renewed()
    {
        var sut = CreateSut(_ => Renewed("next"));

        var (session, _) = await sut.RefreshAsync("own", TestContext.Current.CancellationToken);

        session!.RefreshToken.Should().Be("next");
        sent.Should().Equal("own");
    }

    [Fact]
    public async Task A_Refused_Token_That_Is_Still_Kept_Is_Forgotten()
    {
        storage.Value = StoredSession.Write("old", "admin");
        var sut = CreateSut(_ => Refused());

        var (session, outcome) = await sut.RefreshAsync("old", TestContext.Current.CancellationToken);

        session.Should().BeNull();
        outcome.Should().Be(SessionRenewalOutcome.Refused);
        storage.Value.Should().BeNull();
    }

    [Fact]
    public async Task A_Newer_Session_Kept_By_Another_Tab_While_The_Call_Was_On_Its_Way_Is_Tried_And_Kept()
    {
        storage.Value = StoredSession.Write("old", "admin");
        var sut = CreateSut(token =>
        {
            if (token == "old")
            {
                // Another tab renews and keeps its session meanwhile.
                storage.Value = StoredSession.Write("other-tab", "admin");
                return Refused();
            }

            return token == "other-tab" ? Renewed("after-other-tab") : Refused();
        });

        var (session, outcome) = await sut.RefreshAsync("old", TestContext.Current.CancellationToken);

        outcome.Should().Be(SessionRenewalOutcome.Renewed);
        session!.RefreshToken.Should().Be("after-other-tab");
        sent.Should().Equal("old", "other-tab");
    }

    [Fact]
    public async Task A_Refusal_Does_Not_Forget_A_Session_Another_Tab_Kept_After_The_Check()
    {
        storage.Value = StoredSession.Write("old", "admin");
        storage.BeforeRemove = () => storage.Value = StoredSession.Write("other-tab", "admin");
        var sut = CreateSut(_ => Refused());

        await sut.RefreshAsync("old", TestContext.Current.CancellationToken);

        StoredSession.Read(storage.Value)!.RefreshToken.Should().Be("other-tab", "only the refused session is forgotten");
    }

    [Fact]
    public async Task A_Busy_Or_Absent_Server_Keeps_The_Kept_Session()
    {
        var kept = StoredSession.Write("token", "admin");
        storage.Value = kept;
        var sut = CreateSut(_ => (null, SessionRenewalOutcome.Unavailable));

        var (session, outcome) = await sut.RefreshAsync("token", TestContext.Current.CancellationToken);

        session.Should().BeNull();
        outcome.Should().Be(SessionRenewalOutcome.Unavailable);
        storage.Value.Should().Be(kept);
        sent.Should().Equal("token");
    }

    private static (Session? Session, SessionRenewalOutcome Outcome) Renewed(
        string refreshToken)
        => (new Session(
                AccessToken: "access-" + refreshToken,
                ExpiresInSeconds: 3600,
                RefreshToken: refreshToken,
                UserName: "admin",
                Role: UserRole.Admin,
                MediaToken: "media-" + refreshToken),
            SessionRenewalOutcome.Renewed);

    private static (Session? Session, SessionRenewalOutcome Outcome) Refused()
        => (null, SessionRenewalOutcome.Refused);

    private SessionRefresher CreateSut(
        Func<string, (Session? Session, SessionRenewalOutcome Outcome)> answer)
        => new(storage, (token, _) =>
        {
            sent.Add(token);
            return Task.FromResult(answer(token));
        });

    private sealed class FakeStorage : ISessionStorage
    {
        public string? Value { get; set; }

        public Action? BeforeRemove { get; set; }

        public ValueTask<string?> ReadAsync(CancellationToken cancellationToken)
            => ValueTask.FromResult(Value);

        public ValueTask WriteAsync(
            string value,
            CancellationToken cancellationToken)
        {
            Value = value;
            return ValueTask.CompletedTask;
        }

        public ValueTask RemoveIfAsync(
            string expected,
            CancellationToken cancellationToken)
        {
            BeforeRemove?.Invoke();
            if (string.Equals(Value, expected, StringComparison.Ordinal))
            {
                Value = null;
            }

            return ValueTask.CompletedTask;
        }
    }
}