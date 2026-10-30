namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// Renews the session with the refresh token the browser keeps, which every
/// tab shares. Another tab may have renewed already, so the kept token is the
/// one sent; a refused token forgets the kept session only while it is still
/// the refused one, so a newer session another tab saved stays.
/// </summary>
public sealed class SessionRefresher(
    ISessionStorage storage,
    Func<string, CancellationToken, Task<(Session? Session, SessionRenewalOutcome Outcome)>> refresh)
{
    /// <summary>
    /// Renews the session; the new one is kept for the other tabs and the next visit.
    /// </summary>
    /// <param name="refreshToken">The refresh token this tab holds.</param>
    /// <param name="cancellationToken">Ends the wait.</param>
    public async Task<(Session? Session, SessionRenewalOutcome Outcome)> RefreshAsync(
        string refreshToken,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(refreshToken);

        var kept = await storage.ReadAsync(cancellationToken).ConfigureAwait(false);
        var token = StoredSession.Read(kept)?.RefreshToken ?? refreshToken;

        var (renewed, outcome) = await refresh(token, cancellationToken).ConfigureAwait(false);
        if (SessionRenewal.ForgetsTheStoredSession(outcome))
        {
            // Another tab may have renewed while this call was on its way: one more try with its token.
            var keptToken = StoredSession.Read(await storage.ReadAsync(cancellationToken).ConfigureAwait(false))?.RefreshToken;
            if (keptToken is not null && !string.Equals(keptToken, token, StringComparison.Ordinal))
            {
                token = keptToken;
                (renewed, outcome) = await refresh(token, cancellationToken).ConfigureAwait(false);
            }
        }

        if (renewed is not null)
        {
            await storage.WriteAsync(StoredSession.Write(renewed.RefreshToken, renewed.UserName), cancellationToken).ConfigureAwait(false);
            return (renewed, SessionRenewalOutcome.Renewed);
        }

        if (!SessionRenewal.ForgetsTheStoredSession(outcome))
        {
            return (null, outcome);
        }

        kept = await storage.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (kept is not null && string.Equals(StoredSession.Read(kept)?.RefreshToken, token, StringComparison.Ordinal))
        {
            await storage.RemoveIfAsync(kept, cancellationToken).ConfigureAwait(false);
        }

        return (null, SessionRenewalOutcome.Refused);
    }
}