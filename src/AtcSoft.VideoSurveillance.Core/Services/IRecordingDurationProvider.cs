namespace AtcSoft.VideoSurveillance.Services;

/// <summary>
/// Tells how long a recording is.
/// </summary>
public interface IRecordingDurationProvider
{
    /// <summary>
    /// Gets the length of a finished recording.
    /// </summary>
    /// <param name="filePath">The full path of the recording.</param>
    /// <returns>The length, or <c>null</c> when it cannot be read.</returns>
    TimeSpan? GetDuration(string filePath);
}