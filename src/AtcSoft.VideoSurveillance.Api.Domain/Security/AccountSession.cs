namespace AtcSoft.VideoSurveillance.Api.Domain.Security;

/// <summary>
/// A signed-in session: the access token to send, the refresh token that renews
/// it, and the media token a player or an image adds to a recording's address.
/// </summary>
public sealed record AccountSession(
    string AccessToken,
    TimeSpan AccessTokenLifetime,
    string RefreshToken,
    string UserName,
    AccountRole Role,
    string MediaToken);