namespace AtcSoft.VideoSurveillance.Api.Domain.Security;

/// <summary>
/// What was changed on the server, by whom.
/// </summary>
public interface IAuditLog
{
    /// <summary>
    /// Writes down one change.
    /// </summary>
    void Record(
        string userName,
        string action,
        string target);

    /// <summary>
    /// Gets the latest changes, newest first.
    /// </summary>
    IReadOnlyList<AuditRecord> Recent(int take);
}