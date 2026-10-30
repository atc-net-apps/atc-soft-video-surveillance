namespace AtcSoft.VideoSurveillance.Api.Domain.ApiHandlers.Auth;

/// <summary>
/// Handler business logic for the UpdateOwnPassword operation: the signed-in
/// user's own password, given the current one.
/// </summary>
public sealed class UpdateOwnPasswordHandler(
    IAccountService accounts,
    IHttpContextAccessor httpContextAccessor) : IUpdateOwnPasswordHandler
{
    public Task<UpdateOwnPasswordResult> ExecuteAsync(
        UpdateOwnPasswordParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var caller = CallerIdentity.From(httpContextAccessor.HttpContext?.User);
        if (caller is null)
        {
            return Task.FromResult(UpdateOwnPasswordResult.Unauthorized("Not signed in."));
        }

        var request = parameters.Request;
        return Task.FromResult(accounts.ChangeOwnPassword(caller.UserName, request.CurrentPassword, request.NewPassword)
            ? UpdateOwnPasswordResult.NoContent()
            : UpdateOwnPasswordResult.BadRequest("The current password is not right."));
    }
}