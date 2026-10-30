namespace AtcSoft.VideoSurveillance.Services;

/// <summary>
/// The recordings waiting to be analysed for their activity index (*Analyse recording*), one at a
/// time in the background.
/// </summary>
public interface IRecordingAnalyses
{
    /// <summary>
    /// Queues a recording.
    /// </summary>
    /// <param name="videoPath">The recording, as a full path.</param>
    /// <returns><see langword="false"/> when it is queued or being analysed already.</returns>
    bool TryEnqueue(string videoPath);

    /// <summary>
    /// Gets a value indicating whether a recording is queued or being analysed.
    /// </summary>
    /// <param name="videoPath">The recording, as a full path.</param>
    bool Contains(string videoPath);
}