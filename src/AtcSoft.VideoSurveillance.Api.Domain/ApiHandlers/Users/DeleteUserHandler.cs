namespace AtcSoft.VideoSurveillance.Api.Domain.ApiHandlers.Users;

/// <summary>
/// Handler business logic for the DeleteUser operation.
/// </summary>
public sealed class DeleteUserHandler(IAccountService accounts) : IDeleteUserHandler
{
    public Task<DeleteUserResult> ExecuteAsync(
        DeleteUserParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        return Task.FromResult(accounts.DeleteUser(parameters.UserId) switch
        {
            UserChangeOutcome.Done => DeleteUserResult.NoContent(),
            UserChangeOutcome.LastAdmin => DeleteUserResult.Conflict("The server always keeps one admin; make another user admin first."),
            _ => DeleteUserResult.NotFound($"User {parameters.UserId} not found."),
        });
    }
}