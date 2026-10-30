namespace AtcSoft.VideoSurveillance.Api.Domain.Security;

/// <summary>
/// A role as the API writes it (<c>viewer</c>, <c>operator</c>, <c>admin</c>).
/// </summary>
public static class AccountRoleExtensions
{
    public static string ToApiRoleName(this AccountRole role)
        => role switch
        {
            AccountRole.Viewer => "viewer",
            AccountRole.Operator => "operator",
            AccountRole.Admin => "admin",
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, message: null),
        };

    public static UserRole ToApiRole(this AccountRole role)
        => role switch
        {
            AccountRole.Viewer => UserRole.Viewer,
            AccountRole.Operator => UserRole.Operator,
            AccountRole.Admin => UserRole.Admin,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, message: null),
        };

    public static AccountRole ToAccountRole(this UserRole role)
        => role switch
        {
            UserRole.Viewer => AccountRole.Viewer,
            UserRole.Operator => AccountRole.Operator,
            UserRole.Admin => AccountRole.Admin,
            _ => throw new ArgumentOutOfRangeException(nameof(role), role, message: null),
        };
}