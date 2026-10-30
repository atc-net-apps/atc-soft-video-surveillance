namespace AtcSoft.VideoSurveillance.Wpf.Services;

/// <summary>
/// Gateway service - users, API keys and the audit log (for an admin).
/// </summary>
public sealed partial class GatewayService : IUserAdministration
{
    public async Task<IReadOnlyList<UserAccount>> GetUsersAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await listUsersEndpoint
            .ExecuteAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsOk
            ? [.. result.OkContent]
            : throw Refused(result);
    }

    public async Task<UserAccount> CreateUserAsync(
        string userName,
        string password,
        UserRole role,
        CancellationToken cancellationToken = default)
    {
        var parameters = new CreateUserParameters(Request: new CreateUserRequest(userName, password, role));
        var result = await createUserEndpoint
            .ExecuteAsync(parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsCreated
            ? result.CreatedContent
            : throw Refused(result);
    }

    public async Task<UserAccount> UpdateUserAsync(
        Guid userId,
        UserRole? role,
        string? password,
        CancellationToken cancellationToken = default)
    {
        var parameters = new UpdateUserParameters(UserId: userId, Request: new UpdateUserRequest(Role: role, Password: password));
        var result = await updateUserEndpoint
            .ExecuteAsync(parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsOk
            ? result.OkContent
            : throw Refused(result);
    }

    public async Task DeleteUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var result = await deleteUserEndpoint
            .ExecuteAsync(new DeleteUserParameters(UserId: userId), cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsNoContent)
        {
            throw Refused(result);
        }
    }

    public async Task<IReadOnlyList<ApiKey>> GetApiKeysAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await listApiKeysEndpoint
            .ExecuteAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsOk
            ? [.. result.OkContent]
            : throw Refused(result);
    }

    public async Task<NewApiKey> CreateApiKeyAsync(
        string name,
        UserRole role,
        CancellationToken cancellationToken = default)
    {
        var parameters = new CreateApiKeyParameters(Request: new CreateApiKeyRequest(name, role));
        var result = await createApiKeyEndpoint
            .ExecuteAsync(parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsCreated
            ? result.CreatedContent
            : throw Refused(result);
    }

    public async Task DeleteApiKeyAsync(
        Guid keyId,
        CancellationToken cancellationToken = default)
    {
        var result = await deleteApiKeyEndpoint
            .ExecuteAsync(new DeleteApiKeyParameters(KeyId: keyId), cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsNoContent)
        {
            throw Refused(result);
        }
    }

    public async Task<IReadOnlyList<AuditEntry>> GetAuditEntriesAsync(
        int take,
        CancellationToken cancellationToken = default)
    {
        var result = await listAuditEntriesEndpoint
            .ExecuteAsync(new ListAuditEntriesParameters(Take: take), cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsOk
            ? [.. result.OkContent]
            : throw Refused(result);
    }
}