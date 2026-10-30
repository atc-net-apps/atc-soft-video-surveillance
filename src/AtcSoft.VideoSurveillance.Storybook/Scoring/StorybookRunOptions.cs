namespace AtcSoft.VideoSurveillance.Storybook.Scoring;

/// <summary>
/// How a scene is fed to a detector. The defaults are what the apps do: the picture scaled to
/// 800 on its longer side, the other following its shape (<see cref="MotionAnalysisSize"/>), at
/// the server's rate of 4 a second.
/// </summary>
/// <param name="AnalysisWidth">The longer side the pictures are scaled to.</param>
/// <param name="AnalysisHeight">A height to squeeze the pictures to instead, with <paramref name="AnalysisWidth"/> as the width; <see langword="null"/> keeps their shape.</param>
/// <param name="AnalysesPerSecond">How many pictures a second are analysed.</param>
/// <param name="KeepPictures">Whether each frame keeps the grey picture the detector was given, to be shown (the sample app); a test leaves it off.</param>
public sealed record StorybookRunOptions(
    int AnalysisWidth = MotionAnalysisSize.DefaultLongerSide,
    int? AnalysisHeight = null,
    double AnalysesPerSecond = 4,
    bool KeepPictures = false);