namespace BoundingBoxWpfApp;

/// <summary>
/// What the live clock plays: a video streamed to the player, with its truth when it is a storybook.
/// </summary>
/// <param name="Name">The storybook's name or the recording's file name.</param>
/// <param name="VideoPath">The video streamed.</param>
/// <param name="Truth">The truth of each frame of the video (a storybook), or <see langword="null"/> (a recording).</param>
/// <param name="Fps">The video's frames a second, to find a picture's truth.</param>
/// <param name="VideoWidth">The video's width as stored, before turning.</param>
/// <param name="VideoHeight">The video's height as stored.</param>
/// <param name="DurationSeconds">How long the video is.</param>
/// <param name="Expect">What the storybook expects; empty for a recording.</param>
/// <param name="Detector">How the detector is made (the tuning, the zones, the warm-up).</param>
/// <param name="AnalysesPerSecond">How many pictures a second are analysed.</param>
/// <param name="StoredRotation">How a recording is turned when shown; the stream does not carry it, so the player is told.</param>
public sealed record LiveSource(
    string Name,
    string VideoPath,
    IReadOnlyList<FrameTruth>? Truth,
    double Fps,
    int VideoWidth,
    int VideoHeight,
    double DurationSeconds,
    StorybookExpect Expect,
    SampleDetector Detector,
    double AnalysesPerSecond,
    VideoRotation StoredRotation = VideoRotation.None);