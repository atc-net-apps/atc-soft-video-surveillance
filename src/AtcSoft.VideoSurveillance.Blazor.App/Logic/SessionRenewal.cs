namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// When a browser forgets its stored session: only when the server refuses the refresh token
/// itself (400). A 429 or a server that cannot be reached keeps it, so a reload in a busy
/// minute does not sign the browser out.
/// </summary>
public static class SessionRenewal
{
    /// <summary>
    /// Gets the outcome of a renewal from the answer's status, or <see langword="null"/> without an answer.
    /// </summary>
    public static SessionRenewalOutcome Outcome(int? statusCode)
        => statusCode switch
        {
            >= 200 and < 300 => SessionRenewalOutcome.Renewed,
            400 or 401 => SessionRenewalOutcome.Refused,
            _ => SessionRenewalOutcome.Unavailable,
        };

    public static bool ForgetsTheStoredSession(SessionRenewalOutcome outcome)
        => outcome == SessionRenewalOutcome.Refused;
}