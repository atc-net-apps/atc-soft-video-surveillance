namespace AtcSoft.VideoEngine;

/// <summary>
/// Clockwise rotation applied at the GPU video processor stage.
/// Engine-internal mirror of the higher-level <c>CameraRotation</c>; mapped at the
/// <c>IMediaPipeline</c> boundary so the engine doesn't depend on Core types.
/// </summary>
public enum VideoRotation
{
    /// <summary>No rotation (default).</summary>
    None = 0,

    /// <summary>Rotate the stream 90° clockwise.</summary>
    Rotate90 = 90,

    /// <summary>Rotate the stream 180°.</summary>
    Rotate180 = 180,

    /// <summary>Rotate the stream 270° clockwise.</summary>
    Rotate270 = 270,
}