namespace AtcSoft.VideoEngine.Capture;

/// <summary>
/// A picture of a recording for analysis: grey, one byte per pixel, row by row, at the size asked
/// for, turned the way the picture is displayed.
/// </summary>
/// <param name="Position">How far into the recording it is.</param>
/// <param name="Width">Its width in pixels.</param>
/// <param name="Height">Its height in pixels.</param>
/// <param name="Pixels">Its pixels, <paramref name="Width"/> times <paramref name="Height"/> bytes.</param>
[SuppressMessage("Performance", "CA1819:Properties should not return arrays", Justification = "A picture is its bytes; the detector reads them as they are.")]
public sealed record GreyFrame(
    TimeSpan Position,
    int Width,
    int Height,
    byte[] Pixels);