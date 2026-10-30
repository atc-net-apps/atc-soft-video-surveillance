namespace AtcSoft.VideoSurveillance.Blazor.App.Logic;

/// <summary>
/// What the receiver dialog hands back.
/// </summary>
/// <param name="Url">The address to post to.</param>
/// <param name="Secret">Null to keep the secret as it is, an empty text to remove it, any other to set it.</param>
/// <param name="Events">The events the receiver gets; empty for all.</param>
[SuppressMessage(
    "Design",
    "CA1054:URI parameters should not be strings",
    Justification = "As typed by the user; the server validates it.")]
[SuppressMessage(
    "Design",
    "CA1056:URI properties should not be strings",
    Justification = "As typed by the user; the server validates it.")]
public sealed record WebhookDialogResult(
    string Url,
    string? Secret,
    IReadOnlyList<string> Events);