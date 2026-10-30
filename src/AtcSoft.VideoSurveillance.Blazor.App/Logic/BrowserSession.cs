namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// The signed-in session of this browser: the access token every call sends,
/// and the refresh token that renews it.
/// </summary>
public sealed class BrowserSession(TimeProvider time) : IDisposable
{
    // A token this close to running out is renewed before a call.
    private static readonly TimeSpan RenewAhead = TimeSpan.FromMinutes(1);

    private readonly SemaphoreSlim renewing = new(1, 1);
    private DateTimeOffset expiresAt;
    private DateTimeOffset? retryAfter;

    /// <summary>
    /// Raised when the user signs in, the session is renewed, or it ends.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// Gets how long no renewal is tried after the server was busy or away, so
    /// the calls in between do not run into the renewal's rate limit.
    /// </summary>
    public static TimeSpan UnavailableBackOff { get; } = TimeSpan.FromSeconds(15);

    public string? AccessToken { get; private set; }

    public string? RefreshToken { get; private set; }

    /// <summary>
    /// Gets the token a recording's or a thumbnail's address carries (?media=).
    /// </summary>
    public string? MediaToken { get; private set; }

    public string? UserName { get; private set; }

    public UserRole? Role { get; private set; }

    public bool IsSignedIn
        => AccessToken is not null;

    public bool IsAdmin
        => Role?.Value == UserRole.Names.Admin;

    /// <summary>
    /// Gets a value indicating whether the access token is about to run out.
    /// </summary>
    public bool NeedsRenewal
        => RefreshToken is not null && (AccessToken is null || time.GetUtcNow() >= expiresAt - RenewAhead);

    /// <summary>
    /// Gets or sets how the session is renewed (the auth service sets it).
    /// </summary>
    public Func<CancellationToken, Task<SessionRenewalOutcome>>? Renew { get; set; }

    public void Apply(Session session)
    {
        ArgumentNullException.ThrowIfNull(session);

        retryAfter = null;
        AccessToken = session.AccessToken;
        RefreshToken = session.RefreshToken;
        MediaToken = session.MediaToken;
        UserName = session.UserName;
        Role = session.Role;
        expiresAt = time.GetUtcNow() + TimeSpan.FromSeconds(session.ExpiresInSeconds);
        Changed?.Invoke();
    }

    /// <summary>
    /// Keeps a refresh token from an earlier visit, to be renewed into a session.
    /// </summary>
    public void Restore(
        string refreshToken,
        string? userName)
    {
        AccessToken = null;
        RefreshToken = refreshToken;
        UserName = userName;
    }

    public void Clear()
    {
        retryAfter = null;
        AccessToken = null;
        RefreshToken = null;
        MediaToken = null;
        UserName = null;
        Role = null;
        Changed?.Invoke();
    }

    public void Dispose()
        => renewing.Dispose();

    /// <summary>
    /// Renews the session once, however many calls ask at the same time. A
    /// refused refresh token ends the session; a busy or absent server keeps it,
    /// and no renewal is tried for <see cref="UnavailableBackOff"/>.
    /// </summary>
    /// <param name="cancellationToken">Ends the wait.</param>
    /// <param name="staleToken">The token a call was refused with; already renewed past it, nothing is done.</param>
    public async Task<bool> RenewAsync(
        CancellationToken cancellationToken,
        string? staleToken = null)
    {
        var acquired = false;
        try
        {
            await renewing.WaitAsync(cancellationToken).ConfigureAwait(false);
            acquired = true;

            if (staleToken is not null && AccessToken is not null && !string.Equals(staleToken, AccessToken, StringComparison.Ordinal))
            {
                return true;
            }

            if (staleToken is null && !NeedsRenewal)
            {
                return IsSignedIn;
            }

            if (Renew is null || RefreshToken is null)
            {
                Clear();
                return false;
            }

            if (retryAfter is { } waitUntil && time.GetUtcNow() < waitUntil)
            {
                return false;
            }

            switch (await Renew(cancellationToken).ConfigureAwait(false))
            {
                case SessionRenewalOutcome.Renewed:
                    retryAfter = null;
                    return true;
                case SessionRenewalOutcome.Unavailable:
                    // The call fails, but the user stays signed in.
                    retryAfter = time.GetUtcNow() + UnavailableBackOff;
                    return false;
                default:
                    Clear();
                    return false;
            }
        }
        finally
        {
            if (acquired)
            {
                renewing.Release();
            }
        }
    }
}