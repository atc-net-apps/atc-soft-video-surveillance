namespace AtcSoft.VideoSurveillance.Blazor.App.Services;

/// <summary>
/// Gateway service - sign-in and the signed-in user's own password.
/// </summary>
public sealed partial class GatewayService
{
    /// <summary>
    /// Gets whether the server still needs its first admin; <see langword="null"/>
    /// when it could not be reached.
    /// </summary>
    public async Task<bool?> IsSetupRequiredAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await getSetupStatusEndpoint
            .ExecuteAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsOk
            ? result.OkContent.SetupRequired
            : null;
    }

    public async Task<GatewayResult<Session>> CompleteSetupAsync(
        string setupCode,
        string userName,
        string password,
        CancellationToken cancellationToken = default)
    {
        var parameters = new CompleteSetupParameters(Request: new CompleteSetupRequest(setupCode, userName, password));
        var result = await completeSetupEndpoint
            .ExecuteAsync(parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsOk
            ? GatewayResult.Success(result.OkContent)
            : GatewayResult.Refused<Session>(result);
    }

    public async Task<GatewayResult<Session>> SignInAsync(
        string userName,
        string password,
        CancellationToken cancellationToken = default)
    {
        var parameters = new CreateSessionParameters(Request: new SignInRequest(userName, password));
        var result = await createSessionEndpoint
            .ExecuteAsync(parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsOk
            ? GatewayResult.Success(result.OkContent)
            : GatewayResult.Refused<Session>(result);
    }

    /// <summary>
    /// Renews a session; <see langword="null"/> when the server refused (sign in
    /// again) or could not be reached.
    /// </summary>
    /// <summary>
    /// Renews the session; the outcome tells a refused refresh token (sign in again) from a
    /// server that is busy or away (keep the stored session and try again later).
    /// </summary>
    public async Task<(Session? Session, SessionRenewalOutcome Outcome)> RefreshSessionAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        var parameters = new RefreshSessionParameters(Request: new RefreshSessionRequest(refreshToken));
        try
        {
            var result = await refreshSessionEndpoint
                .ExecuteAsync(parameters, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return result.IsOk
                ? (result.OkContent, SessionRenewalOutcome.Renewed)
                : (null, SessionRenewal.Outcome((int)result.StatusCode));
        }
        catch (Exception ex) when (ex is HttpRequestException or TimeoutException or Polly.ExecutionRejectedException ||
                                   (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            // No server, or the resilience pipeline gave up on it (a timeout, an open circuit).
            return (null, SessionRenewalOutcome.Unavailable);
        }
    }

    public Task EndSessionAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
        => endSessionEndpoint.ExecuteAsync(
            new EndSessionParameters(Request: new RefreshSessionRequest(refreshToken)),
            cancellationToken: cancellationToken);

    public async Task<GatewayResult> ChangeOwnPasswordAsync(
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var parameters = new UpdateOwnPasswordParameters(Request: new ChangePasswordRequest(currentPassword, newPassword));
        var result = await updateOwnPasswordEndpoint
            .ExecuteAsync(parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsNoContent
            ? GatewayResult.Done
            : GatewayResult.Refused(result);
    }
}