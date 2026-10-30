namespace AtcSoft.VideoSurveillance.Api.Domain.ApiHandlers.ApiKeys;

/// <summary>
/// Handler business logic for the ListApiKeys operation: names and roles, never the keys.
/// </summary>
public sealed class ListApiKeysHandler(IAccountService accounts) : IListApiKeysHandler
{
    public Task<ListApiKeysResult> ExecuteAsync(
        CancellationToken cancellationToken = default)
        => Task.FromResult(ListApiKeysResult.Ok([.. accounts.ListApiKeys().Select(k => k.ToApi())]));
}