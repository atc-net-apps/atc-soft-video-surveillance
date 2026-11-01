namespace AtcSoft.Video.Surveillance.Services;

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

    /// <summary>
    /// Gets the length of a finished recording whose size and time of last write are known
    /// already (from a walk of the folder), so the file is not looked at again to tell whether
    /// it changed.
    /// </summary>
    /// <param name="filePath">The full path of the recording.</param>
    /// <param name="length">The file's size.</param>
    /// <param name="lastWriteUtc">The time the file was last written, in UTC.</param>
    /// <returns>The length, or <c>null</c> when it cannot be read.</returns>
    TimeSpan? GetDuration(
        string filePath,
        long length,
        DateTime lastWriteUtc)
        => GetDuration(filePath);
}