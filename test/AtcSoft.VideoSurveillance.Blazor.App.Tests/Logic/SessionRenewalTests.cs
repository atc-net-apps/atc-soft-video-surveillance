namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// A browser forgets its stored session only when the server says the refresh token is no
/// good (400). A refusal for too many tries (429) or a server that cannot be reached keeps it,
/// so a reload in a busy minute does not sign the browser out.
/// </summary>
public sealed class SessionRenewalTests
{
    [Fact]
    public void A_Renewed_Session_Is_Kept()
        => SessionRenewal.Outcome(statusCode: 200).Should().Be(SessionRenewalOutcome.Renewed);

    [Fact]
    public void A_Refused_Refresh_Token_Forgets_The_Session()
    {
        var outcome = SessionRenewal.Outcome(statusCode: 400);

        outcome.Should().Be(SessionRenewalOutcome.Refused);
        SessionRenewal.ForgetsTheStoredSession(outcome).Should().BeTrue();
    }

    [Theory]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(503)]
    [InlineData(null)]
    public void Too_Many_Tries_Or_No_Server_Keeps_The_Session(int? statusCode)
    {
        var outcome = SessionRenewal.Outcome(statusCode);

        outcome.Should().Be(SessionRenewalOutcome.Unavailable);
        SessionRenewal.ForgetsTheStoredSession(outcome).Should().BeFalse();
    }
}