namespace AtcSoft.VideoSurveillance.Models.Settings;

/// <summary>
/// Settings for camera display and grid layout (application-level defaults).
/// </summary>
public class CameraDisplayAppSettings
{
    public bool ShowOverlayTitle { get; set; } = true;

    public bool ShowOverlayDescription { get; set; } = true;

    public bool ShowOverlayTime { get; set; }

    public bool ShowOverlayConnectionStatus { get; set; } = true;

    /// <summary>
    /// Show the quick-action icon buttons (zoom-reset, full-screen) overlaid
    /// on the tile. Each button has its own visibility rules (zoom-reset only
    /// when zoomed; full-screen only on hover) — this toggle gates the whole
    /// quick-action stack.
    /// </summary>
    public bool ShowOverlayQuickActions { get; set; } = true;

    /// <summary>
    /// Show the connection-type icon (LAN / Wi-Fi / USB) in a free corner of
    /// the tile. Opt-in. Cameras whose effective connection type is
    /// <see cref="CameraConnectionType.None"/> show no icon.
    /// </summary>
    public bool ShowOverlayConnectionTypeIcon { get; set; }

    /// <summary>
    /// Which of the two corners adjacent to the info box the connection-type
    /// icon uses. Relative, so it never collides with a per-camera overlay
    /// position override.
    /// </summary>
    public ConnectionTypeIconPlacement ConnectionTypeIconPlacement { get; set; } = ConnectionTypeIconPlacement.SameRow;

    public double OverlayOpacity { get; set; } = 0.7;

    public OverlayPosition OverlayPosition { get; set; } = OverlayPosition.TopLeft;

    public bool AllowDragAndDropReorder { get; set; } = true;

    public bool AutoSaveLayoutChanges { get; set; } = true;

    public string SnapshotPath { get; set; } = ApplicationPaths.DefaultSnapshotsPath;

    /// <inheritdoc />
    public override string ToString()
        => $"CameraDisplayAppSettings {{ Title={ShowOverlayTitle}, Status={ShowOverlayConnectionStatus}, ConnectionTypeIcon={ShowOverlayConnectionTypeIcon}, Opacity={OverlayOpacity.ToString(CultureInfo.InvariantCulture)} }}";
}