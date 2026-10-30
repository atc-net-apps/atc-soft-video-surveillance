namespace AtcSoft.VideoSurveillance.Models;

/// <summary>
/// A picture for motion detection: grey, one byte per pixel row by row, at the analysis size,
/// with the time it shows.
/// </summary>
/// <param name="Gray">The pixels, <paramref name="Width"/> × <paramref name="Height"/> bytes.</param>
/// <param name="Width">The width in pixels.</param>
/// <param name="Height">The height in pixels.</param>
/// <param name="Time">When the picture was taken (live) or where it is in a recording.</param>
[SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "A picture is handed on as it is; copying it per frame would cost more than it protects.")]
public sealed record MotionFrame(
    byte[] Gray,
    int Width,
    int Height,
    DateTimeOffset Time);