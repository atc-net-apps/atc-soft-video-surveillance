namespace AtcSoft.VideoSurveillance.Api.Domain.ApiHandlers.Audit;

/// <summary>
/// Handler business logic for the ListAuditEntries operation: the latest changes first.
/// </summary>
public sealed class ListAuditEntriesHandler(IAuditLog audit) : IListAuditEntriesHandler
{
    private const int DefaultTake = 100;

    public Task<ListAuditEntriesResult> ExecuteAsync(
        ListAuditEntriesParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        return Task.FromResult(ListAuditEntriesResult.Ok(
        [
            .. audit.Recent(parameters.Take ?? DefaultTake).Select(r => new AuditEntry(
                Timestamp: r.Timestamp,
                UserName: r.UserName,
                Action: r.Action,
                Target: r.Target)),
        ]));
    }
}