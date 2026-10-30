namespace AtcSoft.VideoSurveillance.Wpf.App.Models;

/// <summary>
/// Root JSON model for server profile persistence.
/// </summary>
public sealed class ServerProfileData
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2227:Collection properties should be read only", Justification = "Setter required for JSON deserialization")]
    public IList<ServerProfile> Profiles { get; set; } = [];

    public Guid? LastUsedProfileId { get; set; }
}