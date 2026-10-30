namespace AtcSoft.VideoSurveillance.Wpf.Services;

/// <summary>
/// Users, API keys and the audit log, for an admin. A change the server refused
/// throws an <see cref="HttpRequestException"/> with the server's reason.
/// </summary>
public interface IUserAdministration
{
    Task<IReadOnlyList<UserAccount>> GetUsersAsync(
        CancellationToken cancellationToken = default);

    Task<UserAccount> CreateUserAsync(
        string userName,
        string password,
        UserRole role,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes a user's role, password, or both; what is <see langword="null"/> stays.
    /// </summary>
    Task<UserAccount> UpdateUserAsync(
        Guid userId,
        UserRole? role,
        string? password,
        CancellationToken cancellationToken = default);

    Task DeleteUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ApiKey>> GetApiKeysAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Makes an API key; the key itself is in the answer only, never again.
    /// </summary>
    Task<NewApiKey> CreateApiKeyAsync(
        string name,
        UserRole role,
        CancellationToken cancellationToken = default);

    Task DeleteApiKeyAsync(
        Guid keyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the latest entries of the audit log, newest first.
    /// </summary>
    Task<IReadOnlyList<AuditEntry>> GetAuditEntriesAsync(
        int take,
        CancellationToken cancellationToken = default);
}