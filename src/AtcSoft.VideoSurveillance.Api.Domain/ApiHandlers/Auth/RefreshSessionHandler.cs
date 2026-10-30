namespace AtcSoft.VideoSurveillance.Api.Domain.ApiHandlers.Auth;

/// <summary>
/// Handler business logic for the RefreshSession operation: a new access token
/// and refresh token for a refresh token, which cannot be used again.
/// </summary>
public sealed class RefreshSessionHandler(IAccountService accounts) : IRefreshSessionHandler
{
    public Task<RefreshSessionResult> ExecuteAsync(
        RefreshSessionParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var session = accounts.Refresh(parameters.Request.RefreshToken);
        return Task.FromResult(session is not null
            ? RefreshSessionResult.Ok(session.ToApi())
            : RefreshSessionResult.BadRequest("The session has ended; sign in again."));
    }
}