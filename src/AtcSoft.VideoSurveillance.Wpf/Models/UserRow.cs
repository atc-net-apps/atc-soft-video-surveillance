namespace AtcSoft.VideoSurveillance.Wpf.Models;

/// <summary>
/// A user as the users dialog shows it: the role in words, the time in the chosen language.
/// </summary>
public sealed record UserRow(
    UserAccount Account,
    string RoleText)
{
    public Guid Id
        => Account.Id;

    public string UserName
        => Account.UserName;

    public string CreatedText
        => Account.CreatedAt.LocalDateTime.ToString("g", CultureInfo.CurrentCulture);
}