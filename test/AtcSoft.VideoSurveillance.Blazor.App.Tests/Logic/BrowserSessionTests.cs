namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// Only a refused refresh token ends the session. A busy or absent server keeps
/// it, and no renewal is tried for a short while, so the calls in between do
/// not run into the renewal's rate limit.
/// </summary>
public sealed class BrowserSessionTests : IDisposable
{
    private readonly Microsoft.Extensions.Time.Testing.FakeTimeProvider time = new(DateTimeOffset.Parse("2026-10-15T12:00:00Z", CultureInfo.InvariantCulture));
    private readonly BrowserSession sut;
    private int renewals;

    public BrowserSessionTests()
    {
        sut = new BrowserSession(time);
        sut.Apply(Session("first"));
    }

    public void Dispose()
        => sut.Dispose();

    [Fact]
    public async Task A_Refused_Refresh_Token_Ends_The_Session()
    {
        sut.Renew = _ => Answer(SessionRenewalOutcome.Refused);

        var renewed = await sut.RenewAsync(TestContext.Current.CancellationToken, staleToken: "first");

        renewed.Should().BeFalse();
        sut.IsSignedIn.Should().BeFalse();
        sut.RefreshToken.Should().BeNull();
    }

    [Fact]
    public async Task A_Busy_Or_Absent_Server_Keeps_The_Session_And_Is_Not_Asked_Again_For_A_While()
    {
        sut.Renew = _ => Answer(SessionRenewalOutcome.Unavailable);

        var first = await sut.RenewAsync(TestContext.Current.CancellationToken, staleToken: "first");
        var second = await sut.RenewAsync(TestContext.Current.CancellationToken, staleToken: "first");

        first.Should().BeFalse("the call that needed the renewal fails");
        second.Should().BeFalse();
        renewals.Should().Be(1, "no renewal is tried during the back-off");
        sut.IsSignedIn.Should().BeTrue();
        sut.RefreshToken.Should().Be("refresh-first");
    }

    [Fact]
    public async Task After_The_Back_Off_The_Session_Is_Renewed_Again()
    {
        sut.Renew = _ => Answer(SessionRenewalOutcome.Unavailable);
        await sut.RenewAsync(TestContext.Current.CancellationToken, staleToken: "first");
        sut.Renew = _ =>
        {
            renewals++;
            sut.Apply(Session("second"));
            return Task.FromResult(SessionRenewalOutcome.Renewed);
        };

        time.Advance(BrowserSession.UnavailableBackOff);
        var renewed = await sut.RenewAsync(TestContext.Current.CancellationToken, staleToken: "first");

        renewed.Should().BeTrue();
        renewals.Should().Be(2);
        sut.AccessToken.Should().Be("second");
    }

    private static Session Session(string accessToken)
        => new(
            AccessToken: accessToken,
            ExpiresInSeconds: 3600,
            RefreshToken: "refresh-" + accessToken,
            UserName: "admin",
            Role: UserRole.Admin,
            MediaToken: "media-" + accessToken);

    private Task<SessionRenewalOutcome> Answer(SessionRenewalOutcome outcome)
    {
        renewals++;
        return Task.FromResult(outcome);
    }
}