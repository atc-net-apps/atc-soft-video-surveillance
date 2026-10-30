namespace AtcSoft.VideoSurveillance.Api.Domain.Webhooks;

/// <summary>
/// What became of a change to a receiver.
/// </summary>
public enum WebhookChangeOutcome
{
    /// <summary>The change was made and stored.</summary>
    Done,

    /// <summary>There is no receiver with that id.</summary>
    NotFound,

    /// <summary>The receiver comes from the server's configuration.</summary>
    NotEditable,

    /// <summary>The address is not an http or https address.</summary>
    InvalidAddress,
}