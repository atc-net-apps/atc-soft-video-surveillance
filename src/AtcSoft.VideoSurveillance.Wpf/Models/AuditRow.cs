namespace AtcSoft.VideoSurveillance.Wpf.Models;

/// <summary>
/// An entry of the audit log as the users dialog shows it.
/// </summary>
public sealed record AuditRow(
    AuditEntry Entry)
{
    public string TimeText
        => Entry.Timestamp.LocalDateTime.ToString("G", CultureInfo.CurrentCulture);

    public string UserName
        => Entry.UserName;

    public string Action
        => Entry.Action;

    public string Target
        => Entry.Target;
}