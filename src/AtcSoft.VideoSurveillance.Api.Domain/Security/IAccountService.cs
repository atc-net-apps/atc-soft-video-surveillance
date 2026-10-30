namespace AtcSoft.VideoSurveillance.Api.Domain.Security;

/// <summary>
/// The server's users and their sessions.
/// </summary>
public interface IAccountService
{
    /// <summary>
    /// Gets a value indicating whether the server has no user yet, so the first
    /// admin is created with the setup code from the data folder.
    /// </summary>
    bool IsSetupRequired { get; }

    /// <summary>
    /// Creates the first admin, when the code is the one in the data folder and
    /// the server has no user yet, and signs them in.
    /// </summary>
    (SetupOutcome Outcome, AccountSession? Session) CompleteSetup(
        string setupCode,
        string userName,
        string password);

    /// <summary>
    /// Signs a user in; <see langword="null"/> when the name or the password is
    /// not right (which, is not said).
    /// </summary>
    AccountSession? SignIn(
        string userName,
        string password);

    /// <summary>
    /// Renews a session for its refresh token, which is replaced by a new one. For a
    /// short grace window the replaced token gives the same new one again (two tabs,
    /// an answer lost to a reload); used after it, the session ends.
    /// <see langword="null"/> when the token is not known or has run out.
    /// </summary>
    AccountSession? Refresh(string refreshToken);

    /// <summary>
    /// Ends the session a refresh token belongs to (the current token, or one
    /// replaced within its grace window); one that is gone is no error.
    /// </summary>
    void SignOut(string refreshToken);

    /// <summary>
    /// Gets the users, oldest first.
    /// </summary>
    IReadOnlyList<AccountUser> ListUsers();

    /// <summary>
    /// Adds a user; the name is unique, whatever its case.
    /// </summary>
    (UserChangeOutcome Outcome, AccountUser? User) CreateUser(
        string userName,
        string password,
        AccountRole role);

    /// <summary>
    /// Changes a user's role, password, or both (what is <see langword="null"/>
    /// stays). A new password ends the user's sessions; the last admin keeps the role.
    /// </summary>
    (UserChangeOutcome Outcome, AccountUser? User) UpdateUser(
        Guid userId,
        AccountRole? role,
        string? password);

    /// <summary>
    /// Removes a user and ends their sessions; the last admin stays.
    /// </summary>
    UserChangeOutcome DeleteUser(Guid userId);

    /// <summary>
    /// Changes the signed-in user's own password, given the current one; the
    /// user's other sessions end.
    /// </summary>
    bool ChangeOwnPassword(
        string userName,
        string currentPassword,
        string newPassword);

    /// <summary>
    /// Gets the API keys, oldest first.
    /// </summary>
    IReadOnlyList<AccountApiKey> ListApiKeys();

    /// <summary>
    /// Makes an API key; the key itself is given here once and kept only as a hash.
    /// </summary>
    (AccountApiKey Key, string Secret) CreateApiKey(
        string name,
        AccountRole role);

    /// <summary>
    /// Removes an API key; <see langword="false"/> when there is none with the id.
    /// </summary>
    bool DeleteApiKey(Guid keyId);

    /// <summary>
    /// Gets the API key a request sent, or <see langword="null"/> when it is not one of the server's.
    /// </summary>
    AccountApiKey? FindApiKey(string secret);
}