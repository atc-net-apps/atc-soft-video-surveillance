namespace AtcSoft.VideoSurveillance.Services;

/// <summary>
/// Reads which volume a folder is on and how much space is free there. Behind an interface so
/// the disk-space guard can be tested without filling a disk.
/// </summary>
public interface IFreeSpaceReader
{
    /// <summary>
    /// Gets the volume a path is on: a drive, a mounted folder or a network share. The path does
    /// not have to exist yet.
    /// </summary>
    /// <param name="path">A file or folder.</param>
    /// <returns>The volume's root, or <see langword="null"/> when it cannot be found.</returns>
    string? GetVolume(string path);

    /// <summary>
    /// Gets the space free for this process on a volume.
    /// </summary>
    /// <param name="volume">A volume from <see cref="GetVolume"/>.</param>
    /// <returns>The free bytes, or <see langword="null"/> when they cannot be read.</returns>
    long? GetFreeBytes(string volume);
}