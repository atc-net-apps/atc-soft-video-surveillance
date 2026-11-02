namespace AtcSoft.Video.Surveillance.Api.Domain.ApiHandlers.Auth;

/// <summary>
/// Handler business logic for the GetSetupStatus operation: whether the server
/// still needs its first admin, and whether this caller needs the setup code for it.
/// </summary>
public sealed class GetSetupStatusHandler(
    IAccountService accounts,
    LocalSetup localSetup,
    IHttpContextAccessor httpContextAccessor) : IGetSetupStatusHandler
{
    public Task<GetSetupStatusResult> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        var setupRequired = accounts.IsSetupRequired;
        return Task.FromResult(GetSetupStatusResult.Ok(new SetupStatus(
            SetupRequired: setupRequired,
            SetupCodeRequired: setupRequired && !localSetup.Applies(httpContextAccessor.HttpContext),
            SetupCodeFile: LocalSetup.SetupCodeFileName)));
    }
}