namespace AtcSoft.VideoSurveillance.Enums;

/// <summary>
/// Specifies the clockwise rotation applied to a camera stream.
/// The live picture, motion analysis, snapshots and thumbnails are rotated.
/// Recordings keep the camera's own pixels and carry a rotation flag that
/// players apply at playback.
/// </summary>
public enum CameraRotation
{
    /// <summary>No rotation (default).</summary>
    None = 0,

    /// <summary>Rotate the stream 90° clockwise.</summary>
    Rotate90 = 90,

    /// <summary>Rotate the stream 180°.</summary>
    Rotate180 = 180,

    /// <summary>Rotate the stream 270° clockwise (90° counter-clockwise).</summary>
    Rotate270 = 270,
}