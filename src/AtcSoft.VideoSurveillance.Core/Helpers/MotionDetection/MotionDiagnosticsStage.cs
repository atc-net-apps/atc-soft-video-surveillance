namespace AtcSoft.VideoSurveillance.Helpers.MotionDetection;

/// <summary>
/// How far <see cref="BuiltInMotionDetector"/> got with a picture (<see cref="MotionDiagnostics"/>).
/// </summary>
public enum MotionDiagnosticsStage
{
    /// <summary>
    /// The first picture (or one of a new size): it became the background.
    /// </summary>
    Learning,

    /// <summary>
    /// Within the warm-up after the first picture: the scene is learnt, no box is shown.
    /// </summary>
    WarmUp,

    /// <summary>
    /// Most of the picture changed at once: taken as light, the background shifted or learnt anew.
    /// </summary>
    LightingChange,

    /// <summary>
    /// Shortly after a change of light: the background settles, no box is shown.
    /// </summary>
    Settling,

    /// <summary>
    /// Analysed: the foreground, its blobs and the tracks.
    /// </summary>
    Analysed,
}