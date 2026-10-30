namespace AtcSoft.VideoSurveillance.Api.Domain.Security;

/// <summary>
/// Who called, from the claims of their access token (or API key).
/// </summary>
public sealed record CallerIdentity(
    string UserName,
    AccountRole Role)
{
    /// <summary>
    /// The claim the user's name is in.
    /// </summary>
    public const string NameClaim = "name";

    /// <summary>
    /// The claim the user's role is in.
    /// </summary>
    public const string RoleClaim = "role";

    /// <summary>
    /// Gets the caller, or <see langword="null"/> when nobody signed in.
    /// </summary>
    public static CallerIdentity? From(ClaimsPrincipal? user)
    {
        if (user?.Identity?.IsAuthenticated != true ||
            user.FindFirst(NameClaim)?.Value is not { Length: > 0 } name)
        {
            return null;
        }

        AccountRole? role = user.FindFirst(RoleClaim)?.Value switch
        {
            "admin" => AccountRole.Admin,
            "operator" => AccountRole.Operator,
            "viewer" => AccountRole.Viewer,
            _ => null,
        };

        return role is { } known ? new CallerIdentity(name, known) : null;
    }
}