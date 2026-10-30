namespace AtcSoft.VideoSurveillance.Api.Domain.Security;

/// <summary>
/// A session as the API sends it.
/// </summary>
public static class AccountSessionExtensions
{
    public static Session ToApi(this AccountSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        return new Session(
            AccessToken: session.AccessToken,
            ExpiresInSeconds: (int)session.AccessTokenLifetime.TotalSeconds,
            RefreshToken: session.RefreshToken,
            UserName: session.UserName,
            Role: session.Role.ToApiRole(),
            MediaToken: session.MediaToken);
    }
}