namespace AtcSoft.VideoSurveillance.Wpf.App.Models;

/// <summary>
/// Represents a saved server connection profile.
/// </summary>
public sealed class ServerProfile
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1056:URI-like properties should not be strings", Justification = "JSON DTO bound to text controls")]
    public string Url { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DateTimeOffset? LastConnectedAt { get; set; }
}