namespace AtcSoft.VideoSurveillance.Api.Domain.ApiHandlers.Auth;

/// <summary>
/// Handler business logic for the GetCurrentUser operation: who the access token is for.
/// </summary>
public sealed class GetCurrentUserHandler(IHttpContextAccessor httpContextAccessor) : IGetCurrentUserHandler
{
    public Task<GetCurrentUserResult> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        var caller = CallerIdentity.From(httpContextAccessor.HttpContext?.User);
        return Task.FromResult(caller is { } known
            ? GetCurrentUserResult.Ok(new CurrentUser(UserName: known.UserName, Role: known.Role.ToApiRole()))
            : GetCurrentUserResult.Unauthorized("Not signed in."));
    }
}