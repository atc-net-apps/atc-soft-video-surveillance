namespace AtcSoft.VideoSurveillance.Models;

/// <summary>
/// What a recording's activity timeline tells about the place under the mouse.
/// </summary>
/// <param name="Position">The place, as a fraction of the recording, 0 to 1.</param>
/// <param name="Tracks">The most things moving at once there.</param>
/// <param name="Level">The most of the picture that changed there, 0 to 100.</param>
/// <param name="IsAnalysed">Whether any of it was analysed; unknown rather than quiet when not.</param>
/// <param name="Zone">The watch zone something moved in there, or <see langword="null"/>.</param>
public sealed record ActivityHover(
    double Position,
    int Tracks,
    double Level,
    bool IsAnalysed,
    string? Zone);