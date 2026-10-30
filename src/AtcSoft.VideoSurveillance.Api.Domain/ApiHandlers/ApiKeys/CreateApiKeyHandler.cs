namespace AtcSoft.VideoSurveillance.Api.Domain.ApiHandlers.ApiKeys;

/// <summary>
/// Handler business logic for the CreateApiKey operation: the key is in the
/// answer this once.
/// </summary>
public sealed class CreateApiKeyHandler(IAccountService accounts) : ICreateApiKeyHandler
{
    public Task<CreateApiKeyResult> ExecuteAsync(
        CreateApiKeyParameters parameters,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(parameters);

        var (key, secret) = accounts.CreateApiKey(parameters.Request.Name, parameters.Request.Role.ToAccountRole());
        return Task.FromResult(CreateApiKeyResult.Created(
            new NewApiKey(
                Id: key.Id,
                Name: key.Name,
                Role: key.Role.ToApiRole(),
                CreatedAt: key.CreatedAt,
                Key: secret),
            location: $"/api-keys/{key.Id}"));
    }
}