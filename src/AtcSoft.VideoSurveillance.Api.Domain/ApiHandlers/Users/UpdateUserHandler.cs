namespace AtcSoft.VideoSurveillance.Api.Domain.ApiHandlers.Users;

/// <summary>
/// Handler business logic for the UpdateUser operation: what the request
/// leaves out stays.
/// </summary>
public sealed class UpdateUserHandler(IAccountService accounts) : IUpdateUserHandler
{
    public Task<UpdateUserResult> ExecuteAsync(
        UpdateUserParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var request = parameters.Request;
        var (outcome, user) = accounts.UpdateUser(parameters.UserId, request.Role?.ToAccountRole(), request.Password);
        return Task.FromResult(outcome switch
        {
            UserChangeOutcome.Done => UpdateUserResult.Ok(user!.ToApi()),
            UserChangeOutcome.LastAdmin => UpdateUserResult.Conflict("The server always keeps one admin; make another user admin first."),
            _ => UpdateUserResult.NotFound($"User {parameters.UserId} not found."),
        });
    }
}