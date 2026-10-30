namespace AtcSoft.VideoSurveillance.Services;

/// <summary>
/// Finds moving things in a camera's pictures, one picture at a time. One instance per camera:
/// a detector keeps what it learnt from the pictures before (a background, tracks). Both motion
/// services, the sample app and the storybook scorer use only this seam, so a better detector
/// (or, if ever needed, one built on OpenCV in a package of its own) replaces the default
/// without touching them.
/// </summary>
public interface IMotionDetector
{
    /// <summary>
    /// Analyses the next picture.
    /// </summary>
    /// <param name="frame">The picture, grey, at the analysis size.</param>
    /// <returns>What was found.</returns>
    MotionDetectorResult Process(MotionFrame frame);

    /// <summary>
    /// Lets time pass without a picture (a capture failed), so motion can still end.
    /// </summary>
    /// <param name="now">The time now.</param>
    /// <returns>What changed; no boxes.</returns>
    MotionDetectorResult Idle(DateTimeOffset now);

    /// <summary>
    /// Forgets everything learnt, as when the camera reconnects.
    /// </summary>
    void Reset();

    /// <summary>
    /// Takes the camera's zones as they are now (edited, or turned with the picture), keeping
    /// what was learnt. A detector that knows no zones ignores them.
    /// </summary>
    /// <param name="zones">The camera's motion zones.</param>
    void UseZones(IReadOnlyList<MotionZone> zones)
    {
    }
}