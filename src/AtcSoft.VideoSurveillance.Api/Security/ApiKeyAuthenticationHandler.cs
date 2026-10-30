namespace AtcSoft.VideoSurveillance.Api.Security;

/// <summary>
/// Signs in a request that sends an API key (<c>X-API-Key</c>), as the key's
/// name with the key's role.
/// </summary>
public sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    System.Text.Encodings.Web.UrlEncoder encoder,
    IAccountService accounts) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    /// <summary>
    /// The scheme's name, as the specification names it.
    /// </summary>
    public const string SchemeName = "apiKey";

    /// <summary>
    /// The header the key is sent in.
    /// </summary>
    public const string HeaderName = "X-API-Key";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var secret = Request.Headers[HeaderName].ToString();
        if (secret.Length == 0)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        if (accounts.FindApiKey(secret) is not { } key)
        {
            return Task.FromResult(AuthenticateResult.Fail("The API key is not one of the server's."));
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(CallerIdentity.NameClaim, key.Name),
                new Claim(CallerIdentity.RoleClaim, key.Role.ToApiRoleName()),
            ],
            SchemeName,
            CallerIdentity.NameClaim,
            CallerIdentity.RoleClaim);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName)));
    }
}