namespace AtcSoft.VideoSurveillance.Api.Domain.Security;

/// <summary>
/// One change on the server.
/// </summary>
public sealed record AuditRecord(
    DateTimeOffset Timestamp,
    string UserName,
    string Action,
    string Target);