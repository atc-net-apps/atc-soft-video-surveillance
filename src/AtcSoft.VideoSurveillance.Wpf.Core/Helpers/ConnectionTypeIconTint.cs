namespace AtcSoft.VideoSurveillance.Wpf.Core.Helpers;

/// <summary>
/// Link-health tint of the connection-type icon.
/// </summary>
public enum ConnectionTypeIconTint
{
    /// <summary>
    /// Connected (or no state yet): the glyph is drawn normally.
    /// </summary>
    Normal,

    /// <summary>
    /// Connecting / reconnecting: the link is unstable.
    /// </summary>
    Warning,

    /// <summary>
    /// Disconnected / failed.
    /// </summary>
    Error,
}