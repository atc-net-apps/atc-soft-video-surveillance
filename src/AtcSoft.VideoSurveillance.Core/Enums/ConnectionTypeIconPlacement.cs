namespace AtcSoft.VideoSurveillance.Enums;

/// <summary>
/// Placement of the connection-type icon, relative to the overlay info box.
/// The icon always uses one of the two corners adjacent to the info box, so
/// it can never overlap the info box or the quick-action buttons (which sit
/// in the diagonally opposite corner), whatever per-camera overlay position
/// is in effect.
/// </summary>
public enum ConnectionTypeIconPlacement
{
    /// <summary>
    /// Same top/bottom edge as the info box, opposite side (default).
    /// </summary>
    SameRow = 0,

    /// <summary>
    /// Same left/right side as the info box, opposite edge.
    /// </summary>
    SameColumn = 1,
}