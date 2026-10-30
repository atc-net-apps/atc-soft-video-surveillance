namespace AtcSoft.VideoSurveillance.Wpf.Services;

/// <summary>
/// Gateway service - sign-in and the signed-in user's own password. A refusal
/// is answered with the server's reason, not an exception: the dialogs show it.
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
        try
        {
            var result = await getSetupStatusEndpoint
                .ExecuteAsync(cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            return result.IsOk
                ? result.OkContent.SetupRequired
                : null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    public async Task<(Session? Session, string? Problem)> CompleteSetupAsync(
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
            ? (result.OkContent, null)
            : (null, ProblemText.From(result));
    }

    public async Task<(Session? Session, string? Problem)> SignInAsync(
        string userName,
        string password,
        CancellationToken cancellationToken = default)
    {
        var parameters = new CreateSessionParameters(Request: new SignInRequest(userName, password));
        var result = await createSessionEndpoint
            .ExecuteAsync(parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsOk
            ? (result.OkContent, null)
            : (null, ProblemText.From(result));
    }

    /// <summary>
    /// Renews a session. The outcome is <see cref="SessionRenewalOutcome.Refused"/> only when
    /// the server does not know the refresh token (sign in again); a busy or failing server,
    /// or none, is <see cref="SessionRenewalOutcome.Unavailable"/> and keeps the session.
    /// </summary>
    public async Task<(Session? Session, SessionRenewalOutcome Outcome)> RefreshSessionAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var parameters = new RefreshSessionParameters(Request: new RefreshSessionRequest(refreshToken));
            var result = await refreshSessionEndpoint
                .ExecuteAsync(parameters, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

            if (result.IsOk)
            {
                return (result.OkContent, SessionRenewalOutcome.Renewed);
            }

            return result.StatusCode is System.Net.HttpStatusCode.BadRequest or System.Net.HttpStatusCode.Unauthorized
                ? (null, SessionRenewalOutcome.Refused)
                : (null, SessionRenewalOutcome.Unavailable);
        }
        catch (Exception ex) when (ex is HttpRequestException or TimeoutException or Polly.ExecutionRejectedException ||
                                   (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            // No server, or the resilience pipeline gave up on it (a timeout, an open circuit).
            return (null, SessionRenewalOutcome.Unavailable);
        }
    }

    public async Task EndSessionAsync(
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await endSessionEndpoint
                .ExecuteAsync(new EndSessionParameters(Request: new RefreshSessionRequest(refreshToken)), cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            // The session runs out on the server by itself.
        }
    }

    /// <summary>
    /// Changes the signed-in user's own password; returns the server's reason when it refused.
    /// </summary>
    public async Task<string?> ChangeOwnPasswordAsync(
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var parameters = new UpdateOwnPasswordParameters(Request: new ChangePasswordRequest(currentPassword, newPassword));
        var result = await updateOwnPasswordEndpoint
            .ExecuteAsync(parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsNoContent
            ? null
            : ProblemText.From(result);
    }
}