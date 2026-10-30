namespace AtcSoft.VideoSurveillance.Api.Domain.Security;

/// <summary>
/// What a user may do on the server. Each role may do what the one before it may.
/// </summary>
public enum AccountRole
{
    /// <summary>Watches live and recorded video and reads everything else.</summary>
    Viewer,

    /// <summary>Also records, takes snapshots, deletes recordings and edits cameras and layouts.</summary>
    Operator,

    /// <summary>Also changes the settings, the webhooks, the users and the API keys.</summary>
    Admin,
}