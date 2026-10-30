namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// Feeds the generated credentials handler from the signed-in session: a token
/// about to run out is renewed first, and a read refused for its token is sent
/// again after one renewal, however many calls were refused with it.
/// </summary>
public static class SessionCredentials
{
    public static void Configure(
        ApiCredentials credentials,
        BrowserSession session)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(session);

        credentials.AccessToken = async cancellationToken =>
        {
            if (session.NeedsRenewal)
            {
                await session.RenewAsync(cancellationToken).ConfigureAwait(false);
            }

            return session.AccessToken;
        };

        credentials.RenewAccessToken = async (refused, cancellationToken)
            => await session.RenewAsync(cancellationToken, staleToken: refused).ConfigureAwait(false);
    }
}