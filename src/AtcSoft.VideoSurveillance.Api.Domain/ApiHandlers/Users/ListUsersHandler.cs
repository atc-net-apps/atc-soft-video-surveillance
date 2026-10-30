namespace AtcSoft.VideoSurveillance.Api.Domain.ApiHandlers.Users;

/// <summary>
/// Handler business logic for the ListUsers operation.
/// </summary>
public sealed class ListUsersHandler(IAccountService accounts) : IListUsersHandler
{
    public Task<ListUsersResult> ExecuteAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult(ListUsersResult.Ok([.. accounts.ListUsers().Select(u => u.ToApi())]));
}