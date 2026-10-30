namespace AtcSoft.VideoSurveillance.Api.Domain.ApiHandlers.Auth;

/// <summary>
/// Handler business logic for the CompleteSetup operation: the first admin,
/// made with the setup code from the server's data folder.
/// </summary>
public sealed class CompleteSetupHandler(
    IAccountService accounts,
    IAuditLog audit) : ICompleteSetupHandler
{
    public Task<CompleteSetupResult> ExecuteAsync(
        CompleteSetupParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var request = parameters.Request;
        var (outcome, session) = accounts.CompleteSetup(request.SetupCode, request.UserName, request.Password);
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
            _ => CompleteSetupResult.BadRequest("The setup code is not right. It is in the file setup-code.txt in the server's data folder."),
        });
    }
}