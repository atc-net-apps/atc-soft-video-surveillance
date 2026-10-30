namespace AtcSoft.VideoSurveillance.Api.Domain.Security;

/// <summary>
/// A user of the server, as an admin sees it (never the password).
/// </summary>
public sealed record AccountUser(
    Guid Id,
    string UserName,
    AccountRole Role,
    DateTimeOffset CreatedAt);