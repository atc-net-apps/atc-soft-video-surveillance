namespace AtcSoft.VideoSurveillance.Helpers.Activity;

/// <summary>
/// The colour of a gap on a timeline, by its reason: the same in every client.
/// </summary>
public static class TimelineGapColors
{
    /// <summary>
    /// Gets the colour of a reason.
    /// </summary>
    /// <param name="reason">Why nothing was recorded.</param>
    /// <returns>Alpha, red, green and blue, 0 to 255.</returns>
    public static (byte A, byte R, byte G, byte B) Of(GapReason reason)
        => reason switch
        {
            GapReason.Offline => (0x59, 0xE5, 0x39, 0x35),
            GapReason.DiskFull => (0x73, 0xFF, 0xB3, 0x00),
            GapReason.ServerDown => (0x59, 0x7E, 0x57, 0xC2),
            GapReason.NotRecording => (0x59, 0x80, 0x80, 0x80),
            _ => (0x2E, 0x80, 0x80, 0x80),
        };
}