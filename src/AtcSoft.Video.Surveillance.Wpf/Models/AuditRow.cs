namespace AtcSoft.Video.Surveillance.Wpf.Models;

/// <summary>
/// An entry of the audit log as the users dialog shows it.
/// </summary>
public sealed record AuditRow(
    AuditEntry Entry)
{
    /// <summary>
    /// Gets when it happened, in this computer's time zone: the log shows it and filters on it.
    /// </summary>
    public DateTime LocalTime
        => Entry.Timestamp.LocalDateTime;

    public string TimeText
        => Entry.Timestamp.LocalDateTime.ToString("G", CultureInfo.CurrentCulture);

    public string UserName
        => Entry.UserName;

    public string Action
        => Entry.Action;

    public string Target
        => Entry.Target;
}