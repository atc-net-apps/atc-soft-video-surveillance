namespace AtcSoft.VideoSurveillance.Helpers.MotionDetection;

/// <summary>
/// What became of a blob (<see cref="MotionCandidate"/>).
/// </summary>
public enum MotionCandidateState
{
    /// <summary>
    /// Smaller than the smallest object (the camera's, or its watch zone's): left out before the
    /// near blobs are merged.
    /// </summary>
    TooSmall,

    /// <summary>
    /// Followed for less time than confirms a track.
    /// </summary>
    TooNew,

    /// <summary>
    /// Followed long enough, but it has not travelled far enough from where it began (swaying on the spot).
    /// </summary>
    NotTravelled,

    /// <summary>
    /// A confirmed track: shown as a box.
    /// </summary>
    Shown,
}