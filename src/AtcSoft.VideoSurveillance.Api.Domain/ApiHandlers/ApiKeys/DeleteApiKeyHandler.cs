namespace AtcSoft.VideoSurveillance.Api.Domain.ApiHandlers.ApiKeys;

/// <summary>
/// Handler business logic for the DeleteApiKey operation.
/// </summary>
public sealed class DeleteApiKeyHandler(IAccountService accounts) : IDeleteApiKeyHandler
{
    public Task<DeleteApiKeyResult> ExecuteAsync(
        DeleteApiKeyParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        return Task.FromResult(accounts.DeleteApiKey(parameters.KeyId)
            ? DeleteApiKeyResult.NoContent()
            : DeleteApiKeyResult.NotFound($"API key {parameters.KeyId} not found."));
    }
}