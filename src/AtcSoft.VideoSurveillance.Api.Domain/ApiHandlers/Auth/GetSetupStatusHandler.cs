namespace AtcSoft.VideoSurveillance.Api.Domain.ApiHandlers.Auth;

/// <summary>
/// Handler business logic for the GetSetupStatus operation: whether the server
/// still needs its first admin.
/// </summary>
public sealed class GetSetupStatusHandler(IAccountService accounts) : IGetSetupStatusHandler
{
    public Task<GetSetupStatusResult> ExecuteAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult(GetSetupStatusResult.Ok(new SetupStatus(SetupRequired: accounts.IsSetupRequired)));
}