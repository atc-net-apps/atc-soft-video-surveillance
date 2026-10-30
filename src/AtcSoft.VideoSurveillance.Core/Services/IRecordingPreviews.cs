namespace AtcSoft.VideoSurveillance.Services;

/// <summary>
/// The recordings waiting for their preview pictures, made one at a time in the background.
/// </summary>
public interface IRecordingPreviews
{
    /// <summary>
    /// Queues a recording for its previews, in place of any it has.
    /// </summary>
    /// <param name="videoPath">The recording, as a full path.</param>
    /// <returns>
    /// <see langword="false"/> when it is queued or being made already, is still being written, or
    /// previews cannot be made here.
    /// </returns>
    bool TryEnqueue(string videoPath);

    /// <summary>
    /// Gets a value indicating whether a recording is queued or its previews are being made.
    /// </summary>
    /// <param name="videoPath">The recording, as a full path.</param>
    bool Contains(string videoPath);
}