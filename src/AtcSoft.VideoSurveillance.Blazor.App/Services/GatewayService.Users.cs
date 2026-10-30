namespace AtcSoft.VideoSurveillance.Blazor.App.Services;

/// <summary>
/// Gateway service - users, API keys and the audit log (for an admin).
/// </summary>
public sealed partial class GatewayService
{
    public async Task<UserAccount[]?> GetUsersAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await listUsersEndpoint
            .ExecuteAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsOk
            ? result.OkContent.ToArray()
            : null;
    }

    public async Task<GatewayResult<UserAccount>> CreateUserAsync(
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
            ? GatewayResult.Success(result.CreatedContent)
            : GatewayResult.Refused<UserAccount>(result);
    }

    /// <summary>
    /// Changes a user's role, password, or both; what is <see langword="null"/> stays.
    /// </summary>
    public async Task<GatewayResult<UserAccount>> UpdateUserAsync(
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
            ? GatewayResult.Success(result.OkContent)
            : GatewayResult.Refused<UserAccount>(result);
    }

    public async Task<GatewayResult> DeleteUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var result = await deleteUserEndpoint
            .ExecuteAsync(new DeleteUserParameters(UserId: userId), cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsNoContent
            ? GatewayResult.Done
            : GatewayResult.Refused(result);
    }

    public async Task<ApiKey[]?> GetApiKeysAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await listApiKeysEndpoint
            .ExecuteAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsOk
            ? result.OkContent.ToArray()
            : null;
    }

    public async Task<GatewayResult<NewApiKey>> CreateApiKeyAsync(
        string name,
        UserRole role,
        CancellationToken cancellationToken = default)
    {
        var parameters = new CreateApiKeyParameters(Request: new CreateApiKeyRequest(name, role));
        var result = await createApiKeyEndpoint
            .ExecuteAsync(parameters, cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsCreated
            ? GatewayResult.Success(result.CreatedContent)
            : GatewayResult.Refused<NewApiKey>(result);
    }

    public async Task<GatewayResult> DeleteApiKeyAsync(
        Guid keyId,
        CancellationToken cancellationToken = default)
    {
        var result = await deleteApiKeyEndpoint
            .ExecuteAsync(new DeleteApiKeyParameters(KeyId: keyId), cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsNoContent
            ? GatewayResult.Done
            : GatewayResult.Refused(result);
    }

    public async Task<AuditEntry[]?> GetAuditEntriesAsync(
        int take,
        CancellationToken cancellationToken = default)
    {
        var result = await listAuditEntriesEndpoint
            .ExecuteAsync(new ListAuditEntriesParameters(Take: take), cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return result.IsOk
            ? result.OkContent.ToArray()
            : null;
    }
}