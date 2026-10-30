namespace AtcSoft.VideoSurveillance.Wpf.Services;

/// <summary>
/// Feeds the generated credentials handler from the signed-in session.
/// </summary>
public static class SessionCredentials
{
    public static void Configure(
        ApiCredentials credentials,
        ClientSession session)
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