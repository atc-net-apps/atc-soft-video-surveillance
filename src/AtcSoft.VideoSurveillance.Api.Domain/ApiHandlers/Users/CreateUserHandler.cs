namespace AtcSoft.VideoSurveillance.Api.Domain.ApiHandlers.Users;

/// <summary>
/// Handler business logic for the CreateUser operation.
/// </summary>
public sealed class CreateUserHandler(IAccountService accounts) : ICreateUserHandler
{
    public Task<CreateUserResult> ExecuteAsync(
        CreateUserParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var request = parameters.Request;
        var (outcome, user) = accounts.CreateUser(request.UserName, request.Password, request.Role.ToAccountRole());
        return Task.FromResult(outcome == UserChangeOutcome.Done
            ? CreateUserResult.Created(user!.ToApi(), location: $"/users/{user!.Id}")
            : CreateUserResult.Conflict($"A user is called '{request.UserName.Trim()}' already."));
    }
}