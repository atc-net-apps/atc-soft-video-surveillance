namespace AtcSoft.VideoSurveillance.Wpf.Services;

/// <summary>
/// How renewing the session went.
/// </summary>
public enum SessionRenewalOutcome
{
    /// <summary>A new access token and refresh token.</summary>
    Renewed,

    /// <summary>The server does not know the refresh token, or it has run out: sign in again.</summary>
    Refused,

    /// <summary>Too many tries, a server error, or no server: keep the session and try again later.</summary>
    Unavailable,
}