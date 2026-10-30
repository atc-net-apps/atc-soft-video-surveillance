namespace AtcSoft.VideoSurveillance.Blazor.App.Services;

/// <summary>
/// Signs this browser in and out, and keeps the session across a page reload:
/// the refresh token is in the browser's storage, the access token only in memory.
/// </summary>
public sealed class AuthService : IDisposable
{
    private readonly GatewayService gateway;
    private readonly BrowserSession session;
    private readonly IJSRuntime js;
    private readonly BrowserSessionStorage storage;
    private readonly SessionRefresher refresher;
    private DotNetObjectReference<AuthService>? watcher;

    public AuthService(
        GatewayService gateway,
        BrowserSession session,
        IJSRuntime js)
    {
        ArgumentNullException.ThrowIfNull(gateway);
        ArgumentNullException.ThrowIfNull(session);

        this.gateway = gateway;
        this.session = session;
        this.js = js;
        storage = new BrowserSessionStorage(js);
        refresher = new SessionRefresher(storage, gateway.RefreshSessionAsync);
        session.Renew = RenewAsync;
    }

    /// <summary>
    /// Takes up the session of an earlier visit, when there is one the server still has.
    /// </summary>
    public async Task InitializeAsync()
    {
        if (watcher is null)
        {
            watcher = DotNetObjectReference.Create(this);
            try
            {
                await js.InvokeVoidAsync("atcSession.watch", watcher).ConfigureAwait(true);
            }
            catch (JSException)
            {
                // An older session.js still in the browser's cache: the other tabs' sign-out is not seen.
            }
        }

        if (session.IsSignedIn)
        {
            return;
        }

        var stored = await storage.ReadAsync(CancellationToken.None).ConfigureAwait(true);
        if (StoredSession.Read(stored) is { } earlier)
        {
            session.Restore(earlier.RefreshToken, earlier.UserName);
            await session.RenewAsync(CancellationToken.None).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Signs in; returns the server's reason when it refused.
    /// </summary>
    public async Task<string?> SignInAsync(
        string userName,
        string password)
    {
        var result = await gateway.SignInAsync(userName, password).ConfigureAwait(true);
        return await TakeAsync(result).ConfigureAwait(true);
    }

    /// <summary>
    /// Creates the first admin and signs them in; returns the server's reason when it refused.
    /// </summary>
    public async Task<string?> CompleteSetupAsync(
        string setupCode,
        string userName,
        string password)
    {
        var result = await gateway.CompleteSetupAsync(setupCode, userName, password).ConfigureAwait(true);
        return await TakeAsync(result).ConfigureAwait(true);
    }

    public async Task SignOutAsync()
    {
        if (session.RefreshToken is { } refreshToken)
        {
            await gateway.EndSessionAsync(refreshToken).ConfigureAwait(true);
        }

        session.Clear();
        await js.InvokeVoidAsync("atcSession.clear").ConfigureAwait(true);
    }

    /// <summary>
    /// Another tab forgot the session (signed out): this one is signed out too.
    /// </summary>
    [JSInvokable]
    public void OnSignedOutElsewhere()
    {
        if (session.RefreshToken is not null)
        {
            session.Clear();
        }
    }

    public void Dispose()
        => watcher?.Dispose();

    private async Task<string?> TakeAsync(GatewayResult<Session> result)
    {
        if (result.Value is not { } signedIn)
        {
            return result.Problem;
        }

        session.Apply(signedIn);
        await storage.WriteAsync(StoredSession.Write(signedIn.RefreshToken, signedIn.UserName), CancellationToken.None).ConfigureAwait(true);
        return null;
    }

    private async Task<SessionRenewalOutcome> RenewAsync(
        CancellationToken cancellationToken)
    {
        if (session.RefreshToken is not { } refreshToken)
        {
            return SessionRenewalOutcome.Refused;
        }

        // The refresher keeps the new session, and forgets a refused one only while it is still kept.
        var (renewed, outcome) = await refresher.RefreshAsync(refreshToken, cancellationToken).ConfigureAwait(true);
        if (renewed is not null)
        {
            session.Apply(renewed);
        }

        return outcome;
    }
}