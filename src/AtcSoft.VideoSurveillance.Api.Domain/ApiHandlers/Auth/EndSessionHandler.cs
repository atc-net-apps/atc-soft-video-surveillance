namespace AtcSoft.VideoSurveillance.Api.Domain.ApiHandlers.Auth;

/// <summary>
/// Handler business logic for the EndSession operation: sign-out.
/// </summary>
public sealed class EndSessionHandler(IAccountService accounts) : IEndSessionHandler
{
    public Task<EndSessionResult> ExecuteAsync(
        EndSessionParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        accounts.SignOut(parameters.Request.RefreshToken);
        return Task.FromResult(EndSessionResult.NoContent());
    }
}