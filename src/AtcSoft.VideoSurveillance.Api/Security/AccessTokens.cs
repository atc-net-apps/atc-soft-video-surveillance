namespace AtcSoft.VideoSurveillance.Api.Security;

/// <summary>
/// Makes and checks the server's access tokens: JSON web tokens signed with a
/// key of this server's own, kept in its data folder, made on its first start.
/// </summary>
public sealed class AccessTokens
{
    /// <summary>
    /// The issuer and audience of the tokens.
    /// </summary>
    public const string Issuer = "AtcSoft.VideoSurveillance";

    /// <summary>
    /// The audience of a media token: it opens the recordings and nothing else.
    /// </summary>
    public const string MediaAudience = "AtcSoft.VideoSurveillance.Media";

    private const int KeyLength = 64;

    private readonly TimeProvider time;
    private readonly SymmetricSecurityKey key;
    private readonly JsonWebTokenHandler handler = new();

    public AccessTokens(
        string keyPath,
        TimeProvider time)
    {
        ArgumentException.ThrowIfNullOrEmpty(keyPath);

        this.time = time;
        key = new SymmetricSecurityKey(LoadOrCreateKey(keyPath));
        ValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = Issuer,
            ValidAudience = Issuer,
            IssuerSigningKey = key,
            NameClaimType = CallerIdentity.NameClaim,
            RoleClaimType = CallerIdentity.RoleClaim,
            ClockSkew = TimeSpan.FromSeconds(30),
            LifetimeValidator = (notBefore, expires, _, _) =>
            {
                var now = time.GetUtcNow().UtcDateTime;
                return (notBefore is null || notBefore <= now.AddSeconds(30)) &&
                       expires is not null && expires > now.AddSeconds(-30);
            },
        };
    }

    /// <summary>
    /// Gets how long an access token is valid.
    /// </summary>
    public TimeSpan Lifetime { get; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets how long a media token is valid: long enough for a recording to be
    /// played to its end.
    /// </summary>
    public TimeSpan MediaLifetime { get; } = TimeSpan.FromHours(12);

    /// <summary>
    /// Gets what a token must be to be valid here (for the bearer handler).
    /// </summary>
    public TokenValidationParameters ValidationParameters { get; }

    /// <summary>
    /// Makes an access token for a user.
    /// </summary>
    public string Create(
        Guid userId,
        string userName,
        AccountRole role)
    {
        var now = time.GetUtcNow().UtcDateTime;
        return handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = Issuer,
            IssuedAt = now,
            NotBefore = now,
            Expires = now + Lifetime,
            Claims = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["sub"] = userId.ToString(),
                [CallerIdentity.NameClaim] = userName,
                [CallerIdentity.RoleClaim] = role.ToApiRoleName(),
            },
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        });
    }

    /// <summary>
    /// Makes a media token: read-only access to the recordings, for an address a
    /// player or an image opens without a header (?media=).
    /// </summary>
    public string CreateMediaToken(string userName)
    {
        var now = time.GetUtcNow().UtcDateTime;
        return handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = MediaAudience,
            IssuedAt = now,
            NotBefore = now,
            Expires = now + MediaLifetime,
            Claims = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                [CallerIdentity.NameClaim] = userName,
            },
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        });
    }

    /// <summary>
    /// Checks a media token; an access token is not one.
    /// </summary>
    public async Task<bool> IsValidMediaTokenAsync(string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return false;
        }

        var parameters = ValidationParameters.Clone();
        parameters.ValidAudience = MediaAudience;
        var result = await handler.ValidateTokenAsync(token, parameters).ConfigureAwait(false);
        return result.IsValid;
    }

    private static byte[] LoadOrCreateKey(string keyPath)
    {
        if (File.Exists(keyPath) &&
            Convert.TryFromBase64String(File.ReadAllText(keyPath).Trim(), new byte[KeyLength], out var length) &&
            length == KeyLength)
        {
            return Convert.FromBase64String(File.ReadAllText(keyPath).Trim());
        }

        var created = RandomNumberGenerator.GetBytes(KeyLength);
        Directory.CreateDirectory(Path.GetDirectoryName(keyPath)!);
        File.WriteAllText(keyPath, Convert.ToBase64String(created));
        return created;
    }
}