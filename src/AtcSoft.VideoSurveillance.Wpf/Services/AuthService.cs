namespace AtcSoft.VideoSurveillance.Wpf.Services;

/// <summary>
/// Signs the desktop client in and out, and keeps its session between starts
/// (<see cref="SessionStore"/>, encrypted for the Windows user).
/// </summary>
public sealed class AuthService
{
    private readonly GatewayService gateway;
    private readonly ClientSession session;
    private readonly SessionStore store;
    private readonly Uri server;

    public AuthService(
        GatewayService gateway,
        ClientSession session,
        SessionStore store,
        Uri server)
    {
        ArgumentNullException.ThrowIfNull(session);

        this.gateway = gateway;
        this.session = session;
        this.store = store;
        this.server = server;
        session.Renew = RenewAsync;
    }

    /// <summary>
    /// Takes up the session kept from the last start, when the server still has it.
    /// </summary>
    public async Task<bool> RestoreAsync()
    {
        if (session.IsSignedIn)
        {
            return true;
        }

        if (store.Load(server) is not { } kept)
        {
            return false;
        }

        session.Restore(kept.RefreshToken, kept.UserName);
        return await session.RenewAsync(CancellationToken.None).ConfigureAwait(false);
    }

    /// <summary>
    /// Signs in; returns the server's reason when it refused.
    /// </summary>
    public async Task<string?> SignInAsync(
        string userName,
        string password)
    {
        var (signedIn, problem) = await gateway.SignInAsync(userName, password).ConfigureAwait(false);
        Take(signedIn);
        return problem;
    }

    /// <summary>
    /// Creates the first admin and signs them in; returns the server's reason when it refused.
    /// </summary>
    public async Task<string?> CompleteSetupAsync(
        string setupCode,
        string userName,
        string password)
    {
        var (signedIn, problem) = await gateway.CompleteSetupAsync(setupCode, userName, password).ConfigureAwait(false);
        Take(signedIn);
        return problem;
    }

    /// <summary>
    /// Changes the signed-in user's own password. The server ends the user's
    /// sessions, so the client signs in again with the new one.
    /// </summary>
    public async Task<string?> ChangeOwnPasswordAsync(
        string currentPassword,
        string newPassword)
    {
        var problem = await gateway.ChangeOwnPasswordAsync(currentPassword, newPassword).ConfigureAwait(false);
        return problem ?? await SignInAsync(session.UserName ?? string.Empty, newPassword).ConfigureAwait(false);
    }

    public async Task SignOutAsync()
    {
        if (session.RefreshToken is { } refreshToken)
        {
            await gateway.EndSessionAsync(refreshToken).ConfigureAwait(false);
        }

        store.Clear(server);
        session.Clear();
    }

    private void Take(Session? signedIn)
    {
        if (signedIn is null)
        {
            return;
        }

        session.Apply(signedIn);
        store.Save(server, signedIn.RefreshToken, signedIn.UserName);
    }

    // Another instance of the client may have renewed the session already, so the kept
    // refresh token is the one sent. A refused one is forgotten only while it is still kept.
    private async Task<SessionRenewalOutcome> RenewAsync(
        CancellationToken cancellationToken)
    {
        if (session.RefreshToken is not { } refreshToken)
        {
            return SessionRenewalOutcome.Refused;
        }

        var token = store.Load(server)?.RefreshToken ?? refreshToken;
        var (renewed, outcome) = await gateway.RefreshSessionAsync(token, cancellationToken).ConfigureAwait(false);
        if (outcome == SessionRenewalOutcome.Refused &&
            store.Load(server)?.RefreshToken is { } kept &&
            !string.Equals(kept, token, StringComparison.Ordinal))
        {
            // Renewed by another instance while this call was on its way: one more try with its token.
            token = kept;
            (renewed, outcome) = await gateway.RefreshSessionAsync(token, cancellationToken).ConfigureAwait(false);
        }

        if (renewed is not null)
        {
            Take(renewed);
            return SessionRenewalOutcome.Renewed;
        }

        if (outcome == SessionRenewalOutcome.Refused)
        {
            store.ClearIf(server, token);
        }

        return outcome;
    }
}