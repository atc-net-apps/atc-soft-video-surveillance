namespace AtcSoft.VideoSurveillance.Wpf.Models;

/// <summary>
/// An API key as the users dialog shows it (never the key itself).
/// </summary>
public sealed record ApiKeyRow(
    ApiKey Key,
    string RoleText)
{
    public Guid Id
        => Key.Id;

    public string Name
        => Key.Name;

    public string CreatedText
        => Key.CreatedAt.LocalDateTime.ToString("g", CultureInfo.CurrentCulture);
}