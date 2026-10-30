namespace AtcSoft.VideoSurveillance.Api.Domain.Security;

/// <summary>
/// Users and API keys as the API sends them.
/// </summary>
public static class AccountUserExtensions
{
    public static UserAccount ToApi(this AccountUser user)
    {
        ArgumentNullException.ThrowIfNull(user);

        return new UserAccount(
            Id: user.Id,
            UserName: user.UserName,
            Role: user.Role.ToApiRole(),
            CreatedAt: user.CreatedAt);
    }

    public static ApiKey ToApi(this AccountApiKey key)
    {
        ArgumentNullException.ThrowIfNull(key);

        return new ApiKey(
            Id: key.Id,
            Name: key.Name,
            Role: key.Role.ToApiRole(),
            CreatedAt: key.CreatedAt);
    }
}