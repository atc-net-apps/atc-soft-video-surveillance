namespace AtcSoft.VideoSurveillance.Api.Domain.Security;

/// <summary>
/// An API key, as an admin sees it: its name and role, never the key itself.
/// </summary>
public sealed record AccountApiKey(
    Guid Id,
    string Name,
    AccountRole Role,
    DateTimeOffset CreatedAt);