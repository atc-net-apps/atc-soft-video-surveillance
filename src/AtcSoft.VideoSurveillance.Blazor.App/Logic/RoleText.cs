namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// A user's role in words, and the roles to choose from.
/// </summary>
public static class RoleText
{
    /// <summary>
    /// Gets the roles to choose from, least first.
    /// </summary>
    public static IReadOnlyList<UserRole> Choices { get; } = [UserRole.Viewer, UserRole.Operator, UserRole.Admin];

    /// <summary>
    /// Gets a role in the chosen language; one the client does not know is shown as it is.
    /// </summary>
    public static string For(UserRole? role)
        => role?.Value switch
        {
            UserRole.Names.Viewer => Translations.RoleViewer,
            UserRole.Names.Operator => Translations.RoleOperator,
            UserRole.Names.Admin => Translations.RoleAdmin,
            null => string.Empty,
            var other => other,
        };
}