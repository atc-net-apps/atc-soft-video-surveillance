namespace AtcSoft.Video.Surveillance.Api.Domain.ApiHandlers.Auth;

/// <summary>
/// Handler business logic for the CompleteSetup operation: the first admin,
/// made with the setup code from the server's data folder, or without it on
/// the server's own machine (<see cref="LocalSetup"/>).
/// </summary>
public sealed class CompleteSetupHandler(
    IAccountService accounts,
    IAuditLog audit,
    LocalSetup localSetup,
    IHttpContextAccessor httpContextAccessor) : ICompleteSetupHandler
{
    public Task<CompleteSetupResult> ExecuteAsync(
        CompleteSetupParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var request = parameters.Request;
        var setupCodeRequired = !localSetup.Applies(httpContextAccessor.HttpContext);
        var (outcome, session) = accounts.CompleteSetup(request.SetupCode, request.UserName, request.Password, setupCodeRequired);
        if (outcome != SetupOutcome.AlreadySetUp)
        {
            audit.Record(
                request.UserName.Trim(),
                outcome == SetupOutcome.Done ? "completeSetup" : "completeSetupRefused",
                "/auth/setup");
        }

        return Task.FromResult(outcome switch
        {
            SetupOutcome.Done => CompleteSetupResult.Ok(session!.ToApi()),
            SetupOutcome.AlreadySetUp => CompleteSetupResult.Conflict("The server has users already; sign in instead."),
            _ => CompleteSetupResult.BadRequest(
                $"The setup code is not right. It is in the file {LocalSetup.SetupCodeFileName} in the server's data folder " +
                "(readable as administrator); a browser on the server itself needs no code."),
        });
    }
}