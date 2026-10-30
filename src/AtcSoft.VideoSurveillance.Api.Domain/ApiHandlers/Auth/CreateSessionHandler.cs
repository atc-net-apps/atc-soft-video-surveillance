namespace AtcSoft.VideoSurveillance.Api.Domain.ApiHandlers.Auth;

/// <summary>
/// Handler business logic for the CreateSession operation: sign-in.
/// </summary>
public sealed class CreateSessionHandler(
    IAccountService accounts,
    IAuditLog audit) : ICreateSessionHandler
{
    public Task<CreateSessionResult> ExecuteAsync(
        CreateSessionParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        // Which of the two is not right is not said.
        var session = accounts.SignIn(parameters.Request.UserName, parameters.Request.Password);
        audit.Record(
            session?.UserName ?? parameters.Request.UserName.Trim(),
            session is not null ? "createSession" : "createSessionRefused",
            "/auth/token");

        return Task.FromResult(session is not null
            ? CreateSessionResult.Ok(session.ToApi())
            : CreateSessionResult.BadRequest("The user name or the password is not right."));
    }
}