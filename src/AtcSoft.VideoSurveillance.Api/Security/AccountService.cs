namespace AtcSoft.VideoSurveillance.Api.Security;

/// <summary>
/// <see cref="IAccountService"/> over a file in the server's data folder. The
/// passwords are kept as hashes (<see cref="PasswordHasher{TUser}"/>) and the
/// refresh tokens as SHA-256 hashes, so the file gives neither away. While the
/// server has no user, a one-time setup code lies next to it.
/// </summary>
/// <remarks>
/// A refresh token is rotated on every renewal, with a grace window
/// (<see cref="RefreshGraceWindow"/>) for the token it replaced: two tabs waking
/// at once, or a renewal whose answer was lost to a page reload, send the old
/// token again. Within the window the old token gets the session's newest
/// refresh token back (not another rotation), so every caller ends up with the
/// same one. What the window needs (the old hash, the session it belongs to and
/// that session's newest token) is kept in memory only; a replay looks the
/// session up by its id, so it follows any number of rotations since. After the
/// window, the token just before the current one means it was copied: the
/// session ends. The file keeps that previous hash and when it was replaced, so
/// after a restart (which forgets the replays) an old token inside its window is
/// only refused, and one after it still ends the session.
/// </remarks>
public sealed partial class AccountService : IAccountService
{
    /// <summary>
    /// How many characters the setup code has.
    /// </summary>
    public const int SetupCodeLength = 24;

    // Letters and digits that cannot be taken for one another.
    private const string SetupCodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    // What an API key starts with, so one is told from other text (a log, a config file).
    private const string ApiKeyPrefix = "vsk_";

    /// <summary>
    /// How long a rotated refresh token still renews into the session's newest one.
    /// </summary>
    public static readonly TimeSpan RefreshGraceWindow = TimeSpan.FromMinutes(2);

    private static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(30);

    private static readonly JsonSerializerOptions FileOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private readonly Lock sync = new();
    private readonly ServerDataPaths paths;
    private readonly TimeProvider time;
    private readonly ILogger<AccountService> logger;
    private readonly PasswordHasher<StoredUser> hasher = new();
    private readonly StoredAccounts file;

    // In memory only: a rotated token's hash -> its session and how long it still renews.
    private readonly Dictionary<string, GraceEntry> graces = new(StringComparer.Ordinal);

    // In memory only: a session's newest refresh token, kept while a grace entry points at it.
    private readonly Dictionary<Guid, string> newestTokens = [];

    public AccountService(
        ServerDataPaths paths,
        TimeProvider time,
        ILogger<AccountService> logger)
    {
        ArgumentNullException.ThrowIfNull(paths);

        this.paths = paths;
        this.time = time;
        this.logger = logger;
        Tokens = new AccessTokens(paths.SigningKeyPath, time);
        file = SafeJsonFile.TryRead<StoredAccounts>(paths.AccountsPath, FileOptions) ?? new StoredAccounts();

        // A file from before sessions had an id.
        foreach (var session in file.Sessions.Where(s => s.Id == Guid.Empty))
        {
            session.Id = Guid.NewGuid();
        }

        if (file.Users.Count == 0)
        {
            EnsureSetupCode();
        }
    }

    /// <summary>
    /// Gets the server's access tokens.
    /// </summary>
    public AccessTokens Tokens { get; }

    /// <inheritdoc />
    public bool IsSetupRequired
    {
        get
        {
            lock (sync)
            {
                return file.Users.Count == 0;
            }
        }
    }

    /// <inheritdoc />
    public (SetupOutcome Outcome, AccountSession? Session) CompleteSetup(
        string setupCode,
        string userName,
        string password)
    {
        lock (sync)
        {
            if (file.Users.Count > 0)
            {
                return (SetupOutcome.AlreadySetUp, null);
            }

            var expected = ReadSetupCode();
            if (expected is null || !FixedTimeEquals(expected, setupCode.Trim().ToUpperInvariant()))
            {
                LogWrongSetupCode();
                return (SetupOutcome.WrongCode, null);
            }

            var user = new StoredUser
            {
                Id = Guid.NewGuid(),
                UserName = userName.Trim(),
                Role = AccountRole.Admin,
                CreatedAt = time.GetUtcNow(),
            };
            user.PasswordHash = hasher.HashPassword(user, password);
            file.Users.Add(user);
            var session = StartSession(user);
            Save();

            TryDelete(paths.SetupCodePath);
            LogSetupCompleted(user.UserName);
            return (SetupOutcome.Done, session);
        }
    }

    /// <inheritdoc />
    public AccountSession? SignIn(
        string userName,
        string password)
    {
        lock (sync)
        {
            var user = FindUser(userName);
            if (user is null)
            {
                // As long as for a known user, so the time does not tell which.
                hasher.VerifyHashedPassword(new StoredUser(), DummyHash, password);
                LogSignInRefused();
                return null;
            }

            var verified = hasher.VerifyHashedPassword(user, user.PasswordHash, password);
            if (verified == PasswordVerificationResult.Failed)
            {
                LogSignInRefused();
                return null;
            }

            if (verified == PasswordVerificationResult.SuccessRehashNeeded)
            {
                user.PasswordHash = hasher.HashPassword(user, password);
            }

            var session = StartSession(user);
            Save();
            return session;
        }
    }

    /// <inheritdoc />
    public AccountSession? Refresh(string refreshToken)
    {
        lock (sync)
        {
            var now = time.GetUtcNow();
            ForgetEndedGraces(now);
            var hash = HashToken(refreshToken);

            var stored = FindSessionByToken(hash);
            if (stored is not null)
            {
                return Rotate(stored, hash, now);
            }

            if (graces.TryGetValue(hash, out var grace))
            {
                return Replay(grace.SessionId, now);
            }

            var replaced = file.Sessions.Find(s => string.Equals(s.PreviousTokenHash, hash, StringComparison.Ordinal));
            if (replaced is not null)
            {
                EndReusedSession(replaced, now);
            }

            return null;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<AccountUser> ListUsers()
    {
        lock (sync)
        {
            return [.. file.Users.Select(ToUser)];
        }
    }

    /// <inheritdoc />
    public (UserChangeOutcome Outcome, AccountUser? User) CreateUser(
        string userName,
        string password,
        AccountRole role)
    {
        lock (sync)
        {
            var name = userName.Trim();
            if (FindUser(name) is not null)
            {
                return (UserChangeOutcome.NameTaken, null);
            }

            var user = new StoredUser
            {
                Id = Guid.NewGuid(),
                UserName = name,
                Role = role,
                CreatedAt = time.GetUtcNow(),
            };
            user.PasswordHash = hasher.HashPassword(user, password);
            file.Users.Add(user);
            Save();
            return (UserChangeOutcome.Done, ToUser(user));
        }
    }

    /// <inheritdoc />
    public (UserChangeOutcome Outcome, AccountUser? User) UpdateUser(
        Guid userId,
        AccountRole? role,
        string? password)
    {
        lock (sync)
        {
            var user = file.Users.Find(u => u.Id == userId);
            if (user is null)
            {
                return (UserChangeOutcome.NotFound, null);
            }

            if (role is { } newRole && newRole != AccountRole.Admin && IsLastAdmin(user))
            {
                return (UserChangeOutcome.LastAdmin, null);
            }

            if (role is { } changed)
            {
                user.Role = changed;
            }

            if (password is not null)
            {
                user.PasswordHash = hasher.HashPassword(user, password);
                file.Sessions.RemoveAll(s => s.UserId == user.Id);
            }

            Save();
            return (UserChangeOutcome.Done, ToUser(user));
        }
    }

    /// <inheritdoc />
    public UserChangeOutcome DeleteUser(Guid userId)
    {
        lock (sync)
        {
            var user = file.Users.Find(u => u.Id == userId);
            if (user is null)
            {
                return UserChangeOutcome.NotFound;
            }

            if (IsLastAdmin(user))
            {
                return UserChangeOutcome.LastAdmin;
            }

            file.Users.Remove(user);
            file.Sessions.RemoveAll(s => s.UserId == user.Id);
            Save();
            return UserChangeOutcome.Done;
        }
    }

    /// <inheritdoc />
    public bool ChangeOwnPassword(
        string userName,
        string currentPassword,
        string newPassword)
    {
        lock (sync)
        {
            var user = FindUser(userName);
            if (user is null ||
                hasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword) == PasswordVerificationResult.Failed)
            {
                return false;
            }

            user.PasswordHash = hasher.HashPassword(user, newPassword);
            file.Sessions.RemoveAll(s => s.UserId == user.Id);
            Save();
            return true;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<AccountApiKey> ListApiKeys()
    {
        lock (sync)
        {
            return [.. file.ApiKeys.Select(ToApiKey)];
        }
    }

    /// <inheritdoc />
    public (AccountApiKey Key, string Secret) CreateApiKey(
        string name,
        AccountRole role)
    {
        lock (sync)
        {
            var secret = ApiKeyPrefix + Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(32));
            var key = new StoredApiKey
            {
                Id = Guid.NewGuid(),
                Name = name.Trim(),
                Role = role,
                KeyHash = HashToken(secret),
                CreatedAt = time.GetUtcNow(),
            };
            file.ApiKeys.Add(key);
            Save();
            return (ToApiKey(key), secret);
        }
    }

    /// <inheritdoc />
    public bool DeleteApiKey(Guid keyId)
    {
        lock (sync)
        {
            if (file.ApiKeys.RemoveAll(k => k.Id == keyId) == 0)
            {
                return false;
            }

            Save();
            return true;
        }
    }

    /// <inheritdoc />
    public AccountApiKey? FindApiKey(string secret)
    {
        if (string.IsNullOrEmpty(secret) || !secret.StartsWith(ApiKeyPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var hash = HashToken(secret);
        lock (sync)
        {
            var key = file.ApiKeys.Find(k => FixedTimeEquals(k.KeyHash, hash));
            return key is null ? null : ToApiKey(key);
        }
    }

    /// <inheritdoc />
    public void SignOut(string refreshToken)
    {
        lock (sync)
        {
            ForgetEndedGraces(time.GetUtcNow());
            var hash = HashToken(refreshToken);

            // The current token, or one replaced within its grace window.
            var stored = FindSessionByToken(hash);
            if (stored is null && graces.TryGetValue(hash, out var grace))
            {
                stored = file.Sessions.Find(s => s.Id == grace.SessionId);
            }

            if (stored is not null)
            {
                file.Sessions.Remove(stored);
                ForgetGraces(stored.Id);
                Save();
            }
        }
    }

    private static AccountUser ToUser(StoredUser user)
        => new(user.Id, user.UserName, user.Role, user.CreatedAt);

    private static AccountApiKey ToApiKey(StoredApiKey key)
        => new(key.Id, key.Name, key.Role, key.CreatedAt);

    private static string DummyHash { get; } = new PasswordHasher<StoredUser>().HashPassword(new StoredUser(), "no such user");

    private static string NewRefreshToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private static string HashToken(string token)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static bool FixedTimeEquals(
        string expected,
        string given)
        => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(given));

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // The server has a user now; a code left behind opens nothing.
        }
    }

    // Called under the lock.
    private StoredUser? FindUser(string userName)
        => file.Users.Find(u => string.Equals(u.UserName, userName.Trim(), StringComparison.OrdinalIgnoreCase));

    // Called under the lock.
    private bool IsLastAdmin(StoredUser user)
        => user.Role == AccountRole.Admin && file.Users.Count(u => u.Role == AccountRole.Admin) == 1;

    // Called under the lock.
    private AccountSession StartSession(StoredUser user)
    {
        var now = time.GetUtcNow();
        file.Sessions.RemoveAll(s => s.ExpiresAt <= now);

        var refreshToken = NewRefreshToken();
        file.Sessions.Add(new StoredSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = HashToken(refreshToken),
            ExpiresAt = now + SessionLifetime,
        });

        return CreateSession(user, refreshToken);
    }

    // Called under the lock: gives a session a new refresh token, and keeps the
    // one it replaces renewing into it for the grace window.
    private AccountSession? Rotate(
        StoredSession stored,
        string oldHash,
        DateTimeOffset now)
    {
        var user = file.Users.Find(u => u.Id == stored.UserId);
        if (user is null || stored.ExpiresAt <= now)
        {
            file.Sessions.Remove(stored);
            ForgetGraces(stored.Id);
            Save();
            return null;
        }

        file.Sessions.RemoveAll(s => s.ExpiresAt <= now);

        var refreshToken = NewRefreshToken();
        stored.PreviousTokenHash = oldHash;
        stored.TokenHash = HashToken(refreshToken);
        stored.RotatedAt = now;
        stored.ExpiresAt = now + SessionLifetime;
        graces[oldHash] = new GraceEntry(stored.Id, now + RefreshGraceWindow);
        newestTokens[stored.Id] = refreshToken;
        Save();

        return CreateSession(user, refreshToken);
    }

    // Called under the lock: a rotated token within its grace window gets the
    // session's newest refresh token again, with fresh access and media tokens.
    private AccountSession? Replay(
        Guid sessionId,
        DateTimeOffset now)
    {
        var stored = file.Sessions.Find(s => s.Id == sessionId);
        var user = stored is null ? null : file.Users.Find(u => u.Id == stored.UserId);
        if (stored is null ||
            user is null ||
            stored.ExpiresAt <= now ||
            !newestTokens.TryGetValue(sessionId, out var refreshToken))
        {
            return null;
        }

        return CreateSession(user, refreshToken);
    }

    // Called under the lock: the token before the current one, sent again after
    // its grace window, was most likely copied, so the session ends. Inside the
    // window (the replays were lost to a restart) it is only refused.
    private void EndReusedSession(
        StoredSession stored,
        DateTimeOffset now)
    {
        if (stored.RotatedAt is { } rotatedAt && now - rotatedAt <= RefreshGraceWindow)
        {
            return;
        }

        file.Sessions.Remove(stored);
        ForgetGraces(stored.Id);
        Save();

        var userName = file.Users.Find(u => u.Id == stored.UserId)?.UserName ?? string.Empty;
        LogRefreshTokenReused(userName);
    }

    // Called under the lock.
    private StoredSession? FindSessionByToken(string hash)
        => file.Sessions.Find(s => string.Equals(s.TokenHash, hash, StringComparison.Ordinal));

    // Called under the lock.
    private void ForgetEndedGraces(DateTimeOffset now)
    {
        foreach (var ended in graces.Where(g => g.Value.Until <= now).Select(g => g.Key).ToList())
        {
            graces.Remove(ended);
        }

        foreach (var unused in newestTokens.Keys.Where(id => !graces.Values.Any(g => g.SessionId == id)).ToList())
        {
            newestTokens.Remove(unused);
        }
    }

    // Called under the lock.
    private void ForgetGraces(Guid sessionId)
    {
        foreach (var ended in graces.Where(g => g.Value.SessionId == sessionId).Select(g => g.Key).ToList())
        {
            graces.Remove(ended);
        }

        newestTokens.Remove(sessionId);
    }

    private AccountSession CreateSession(
        StoredUser user,
        string refreshToken)
        => new(
            Tokens.Create(user.Id, user.UserName, user.Role),
            Tokens.Lifetime,
            refreshToken,
            user.UserName,
            user.Role,
            Tokens.CreateMediaToken(user.UserName));

    private void EnsureSetupCode()
    {
        if (ReadSetupCode() is null)
        {
            var code = new string(RandomNumberGenerator.GetItems(SetupCodeAlphabet.AsSpan(), SetupCodeLength));
            Directory.CreateDirectory(Path.GetDirectoryName(paths.SetupCodePath)!);
            File.WriteAllText(paths.SetupCodePath, code);
        }

        // Where the code is, never the code itself.
        LogSetupRequired(paths.SetupCodePath);
    }

    private string? ReadSetupCode()
    {
        try
        {
            var code = File.ReadAllText(paths.SetupCodePath).Trim().ToUpperInvariant();
            return code.Length == SetupCodeLength ? code : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private void Save()
    {
        if (!SafeJsonFile.TryWrite(paths.AccountsPath, file, FileOptions))
        {
            LogSaveFailed(paths.AccountsPath);
        }
    }

    // What the file holds.
    private sealed class StoredAccounts
    {
        public List<StoredUser> Users { get; set; } = [];

        public List<StoredSession> Sessions { get; set; } = [];

        public List<StoredApiKey> ApiKeys { get; set; } = [];
    }

    private sealed class StoredApiKey
    {
        public Guid Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public AccountRole Role { get; set; }

        // The SHA-256 hash of the key, never the key itself.
        public string KeyHash { get; set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; set; }
    }

    private sealed class StoredUser
    {
        public Guid Id { get; set; }

        public string UserName { get; set; } = string.Empty;

        public AccountRole Role { get; set; }

        public string PasswordHash { get; set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; set; }
    }

    private sealed class StoredSession
    {
        // Stays the same while the refresh token is rotated.
        public Guid Id { get; set; }

        public Guid UserId { get; set; }

        // The SHA-256 hash of the refresh token, never the token itself.
        public string TokenHash { get; set; } = string.Empty;

        // The hash of the token the current one replaced, to recognise it used again.
        public string? PreviousTokenHash { get; set; }

        // When the current token replaced the previous one.
        public DateTimeOffset? RotatedAt { get; set; }

        public DateTimeOffset ExpiresAt { get; set; }
    }

    private sealed record GraceEntry(
        Guid SessionId,
        DateTimeOffset Until);
}